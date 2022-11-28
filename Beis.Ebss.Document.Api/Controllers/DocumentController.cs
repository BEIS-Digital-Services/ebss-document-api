using System.Net;
using Beis.Ebss.Document.Api.Attributes;
using Beis.Ebss.Document.Api.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using nClam;

namespace Beis.Ebss.Document.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class DocumentController : ControllerBase
{
    private readonly ILogger<DocumentController> _logger;
    private readonly DocumentOptions _documentOptions;
    private readonly ClamAVOptions _ClamAvOptions;

    // For more file signatures, see the File Signatures Database (https://www.filesignatures.net/)
    // and the official specifications for the file types you wish to add.
    private readonly Dictionary<string, List<byte[]>> _fileSignature = new Dictionary<string, List<byte[]>>
    {
        { ".png", new List<byte[]> { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } } },
        { ".jpeg", new List<byte[]>
            {
                new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 },
                new byte[] { 0xFF, 0xD8, 0xFF, 0xE2 },
                new byte[] { 0xFF, 0xD8, 0xFF, 0xE3 },
            }
        },
        { ".jpg", new List<byte[]>
            {
                new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 },
                new byte[] { 0xFF, 0xD8, 0xFF, 0xE1 },
                new byte[] { 0xFF, 0xD8, 0xFF, 0xE8 },
            }
        }
    };

    public DocumentController(
        ILogger<DocumentController> logger, 
        IOptions<DocumentOptions> documentOptions,
        IOptions<ClamAVOptions> clamAVOptions)
    {
        this._logger = logger;
        this._documentOptions = documentOptions.Value;
        this._ClamAvOptions = clamAVOptions.Value;
    }

    [HttpGet(Name = "Get")]
    public async Task<IEnumerable<string>> Get()
    {
        this._logger.LogInformation("DocumentController:Get");
        return await Task.FromResult(Enumerable.Range(1, 5).Select(index => new string($"Documents {index}")).ToArray());
    }

    [HttpPost("upload")]
    [DisableFormValueModelBinding]
    public async Task<IActionResult> Upload()
    {
        if (!this.IsMultipartContentType(Request.ContentType))
        {
            this._logger.LogError("File:The request couldn't be processed");
            // Log error
    
            return BadRequest(ModelState);
        }
    
        var boundary = this.GetBoundary(
            MediaTypeHeaderValue.Parse(Request.ContentType),this._documentOptions.MaxFileSize);
        var reader = new MultipartReader(boundary, HttpContext.Request.Body);
        var section = await reader.ReadNextSectionAsync();
    
        while (section != null)
        {
            var hasContentDispositionHeader =
                ContentDispositionHeaderValue.TryParse(
                    section.ContentDisposition, out var contentDisposition);
    
            if (hasContentDispositionHeader)
            {
                // This check assumes that there's a file
                // present without form data. If form data
                // is present, this method immediately fails
                // and returns the model error.
                if (!this.HasFileContentDisposition(contentDisposition))
                {
                    this._logger.LogError("File: The request couldn't be processed.");
                    // Log error
    
                    return BadRequest(ModelState);
                }
                else
                {
                    // Don't trust the file name sent by the client. To display
                    // the file name, HTML-encode the value.
                    var trustedFileNameForDisplay = WebUtility.HtmlEncode(
                        contentDisposition.FileName.Value);
                    var trustedFileNameForFileStorage = Path.GetRandomFileName();
    
                    // **WARNING!**
                    // In the following example, the file is saved without
                    // scanning the file's contents. In most production
                    // scenarios, an anti-virus/anti-malware scanner API
                    // is used on the file before making the file available
                    // for download or for use by other systems. 
                    // For more information, see the topic that accompanies 
                    // this sample.
    
                    var streamedFileContent = await this.ProcessStreamedFile(
                        section, contentDisposition, this._documentOptions.AcceptableFileExtensions, this._documentOptions.MaxFileSize);
    
                    if (streamedFileContent.Length == 0)
                    {
                        return BadRequest("Failed see logs.");
                    }
                    
                    try  
                    {  
                        // scan for virus...
                        
                        this._logger.LogInformation("ClamAV scan begin for file {0}", trustedFileNameForFileStorage);  
                        var clam = new ClamClient(this._ClamAvOptions.Url, Convert.ToInt32(this._ClamAvOptions.Port));  
                        var scanResult = await clam.SendAndScanFileAsync(streamedFileContent);    
                        
                        switch (scanResult.Result)  
                        {  
                            case ClamScanResults.Clean:  
                                this._logger.LogInformation("The file is clean! ScanResult:{1}", scanResult.RawResult);  
                                break;  
                            case ClamScanResults.VirusDetected:  
                                this._logger.LogError("Virus Found! Virus name: {1}", scanResult.InfectedFiles.FirstOrDefault().VirusName);  
                                break;  
                            case ClamScanResults.Error:  
                                this._logger.LogError("An error occured while scaning the file! ScanResult: {1}", scanResult.RawResult);  
                                break;  
                            case ClamScanResults.Unknown:  
                                this._logger.LogError("Unknown scan result while scaning the file! ScanResult: {0}", scanResult.RawResult);  
                                break;  
                        }  
                    }  
                    catch (Exception ex)  
                    {  
  
                        this._logger.LogError("ClamAV Scan Exception: {0}", ex.ToString());  
                    }

                    using (var targetStream = System.IO.File.Create(
                               Path.Combine(this._documentOptions.LocalStoragePath, trustedFileNameForFileStorage)))
                    {
                        await targetStream.WriteAsync(streamedFileContent);
    
                        _logger.LogInformation(
                            "Uploaded file '{TrustedFileNameForDisplay}' saved to " +
                            "'{TargetFilePath}' as {TrustedFileNameForFileStorage}",
                            trustedFileNameForDisplay, this._documentOptions.LocalStoragePath,
                            trustedFileNameForFileStorage);
                    }
                }
            }
    
            // Drain any remaining section body that hasn't been consumed and
            // read the headers for the next section.
            section = await reader.ReadNextSectionAsync();
        }
    
        return await Task.FromResult(Ok());
    }
    
    private async Task<byte[]> ProcessStreamedFile(MultipartSection section, ContentDispositionHeaderValue contentDisposition, string[] permittedExtensions, long sizeLimit)
    {
        try
        {
            using (var memoryStream = new MemoryStream())
            {
                await section.Body.CopyToAsync(memoryStream);
    
                // Check if the file is empty or exceeds the size limit.
                if (memoryStream.Length == 0)
                {
                    this._logger.LogError("File: The file is empty.");
                }
                else if (memoryStream.Length > sizeLimit)
                {
                    var megabyteSizeLimit = sizeLimit / 1048576;
                    this._logger.LogError($"File: The file exceeds {megabyteSizeLimit:N1} MB.");
                }
                else if (!IsValidFileExtensionAndSignature(
                             contentDisposition.FileName.Value, memoryStream, 
                             permittedExtensions))
                {
                    this._logger.LogError("File: The file type isn't permitted or the file's " +
                                          "signature doesn't match the file's extension.");
                }
                else
                {
                    return memoryStream.ToArray();
                }
            }
        }
        catch (Exception ex)
        {
            // modelState.AddModelError("File",
            //     "The upload failed. Please contact the Help Desk " +
            //     $" for support. Error: {ex.HResult}");
            throw ex;
            // Log the exception
        }
    
        return Array.Empty<byte>();
    }


    private bool IsValidFileExtensionAndSignature(string fileName, Stream data, string[] permittedExtensions)
    {
        if (string.IsNullOrEmpty(fileName) || data == null || data.Length == 0)
        {
            return false;
        }

        var ext = Path.GetExtension(fileName).ToLowerInvariant();

        if (string.IsNullOrEmpty(ext) || !permittedExtensions.Contains(ext))
        {
            return false;
        }

        data.Position = 0;

        using (var reader = new BinaryReader(data))
        {
            // Uncomment the following code block if you must permit
            // files whose signature isn't provided in the _fileSignature
            // dictionary. We recommend that you add file signatures
            // for files (when possible) for all file types you intend
            // to allow on the system and perform the file signature
            // check.
            /*
            if (!_fileSignature.ContainsKey(ext))
            {
                return true;
            }
            */

            // File signature check
            // --------------------
            // With the file signatures provided in the _fileSignature
            // dictionary, the following code tests the input content's
            // file signature.
            var signatures = _fileSignature[ext];
            var headerBytes = reader.ReadBytes(signatures.Max(m => m.Length));

            return signatures.Any(signature =>
                headerBytes.Take(signature.Length).SequenceEqual(signature));
        }
    }

    // Content-Type: multipart/form-data; boundary="----WebKitFormBoundarymx2fSWqWSd0OxQqq"
    // The spec at https://tools.ietf.org/html/rfc2046#section-5.1 states that 70 characters is a reasonable limit.
    private string GetBoundary(MediaTypeHeaderValue contentType, int lengthLimit)
    {
        var boundary = HeaderUtilities.RemoveQuotes(contentType.Boundary).Value;

        if (string.IsNullOrWhiteSpace(boundary))
        {
            throw new InvalidDataException("Missing content-type boundary.");
        }

        if (boundary.Length > lengthLimit)
        {
            throw new InvalidDataException(
                $"Multipart boundary length limit {lengthLimit} exceeded.");
        }

        return boundary;
    }

    private bool IsMultipartContentType(string contentType)
    {
        return !string.IsNullOrEmpty(contentType)
               && contentType.IndexOf("multipart/", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private bool HasFormDataContentDisposition(ContentDispositionHeaderValue contentDisposition)
    {
        // Content-Disposition: form-data; name="key";
        return contentDisposition != null
            && contentDisposition.DispositionType.Equals("form-data")
            && string.IsNullOrEmpty(contentDisposition.FileName.Value)
            && string.IsNullOrEmpty(contentDisposition.FileNameStar.Value);
    }

    private bool HasFileContentDisposition(ContentDispositionHeaderValue contentDisposition)
    {
        // Content-Disposition: form-data; name="myfile1"; filename="Misc 002.jpg"
        return contentDisposition != null
               && contentDisposition.DispositionType.Equals("form-data")
               && (!string.IsNullOrEmpty(contentDisposition.FileName.Value)
                   || !string.IsNullOrEmpty(contentDisposition.FileNameStar.Value));
    }
}