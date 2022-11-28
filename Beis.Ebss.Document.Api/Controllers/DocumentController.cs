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
    private readonly ILogger<DocumentController> logger;
    private readonly DocumentOptions documentOptions;
    private readonly ClamAVOptions clamAvOptions;

    public DocumentController(
        ILogger<DocumentController> logger, 
        IOptions<DocumentOptions> documentOptions,
        IOptions<ClamAVOptions> clamAvOptions)
    {
        this.logger = logger;
        this.documentOptions = documentOptions.Value;
        this.clamAvOptions = clamAvOptions.Value;
    }

    [HttpGet(Name = "Get")]
    public async Task<IEnumerable<string>> Get()
    {
        this.logger.LogInformation("DocumentController:Get");
        return await Task.FromResult(Enumerable.Range(1, 5).Select(index => new string($"Documents {index}")).ToArray());
    }

    [HttpPost("upload")]
    [DisableFormValueModelBinding]
    public async Task<IActionResult> Upload()
    {
        if (!this.IsMultipartContentType(Request.ContentType))
        {
            this.logger.LogError("File:The request couldn't be processed");
            // Log error
    
            return BadRequest(ModelState);
        }
    
        var boundary = this.GetBoundary(
            MediaTypeHeaderValue.Parse(Request.ContentType),this.documentOptions.MaxFileSize);
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
                    this.logger.LogError("File: The request couldn't be processed.");
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
                        section, contentDisposition, this.documentOptions.AcceptableFileExtensions, this.documentOptions.MaxFileSize);
    
                    if (streamedFileContent.Length == 0)
                    {
                        return BadRequest("Failed see logs.");
                    }
                    
                    try  
                    {  
                        // scan for virus...
                        
                        this.logger.LogInformation("ClamAV scan begin for file {0}", trustedFileNameForFileStorage);  
                        var clam = new ClamClient(this.clamAvOptions.Url, Convert.ToInt32(this.clamAvOptions.Port));  
                        var scanResult = await clam.SendAndScanFileAsync(streamedFileContent);    
                        
                        switch (scanResult.Result)  
                        {  
                            case ClamScanResults.Clean:  
                                this.logger.LogInformation("The file is clean! ScanResult:{1}", scanResult.RawResult);  
                                break;  
                            case ClamScanResults.VirusDetected:  
                                this.logger.LogError("Virus Found! Virus name: {1}", scanResult.InfectedFiles.FirstOrDefault().VirusName);  
                                break;  
                            case ClamScanResults.Error:  
                                this.logger.LogError("An error occured while scaning the file! ScanResult: {1}", scanResult.RawResult);  
                                break;  
                            case ClamScanResults.Unknown:  
                                this.logger.LogError("Unknown scan result while scaning the file! ScanResult: {0}", scanResult.RawResult);  
                                break;  
                        }  
                    }  
                    catch (Exception ex)  
                    {  
  
                        this.logger.LogError("ClamAV Scan Exception: {0}", ex.ToString());  
                    }

                    using (var targetStream = System.IO.File.Create(
                               Path.Combine(this.documentOptions.LocalStoragePath, trustedFileNameForFileStorage)))
                    {
                        await targetStream.WriteAsync(streamedFileContent);
    
                        logger.LogInformation(
                            "Uploaded file '{TrustedFileNameForDisplay}' saved to " +
                            "'{TargetFilePath}' as {TrustedFileNameForFileStorage}",
                            trustedFileNameForDisplay, this.documentOptions.LocalStoragePath,
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
                    this.logger.LogError("File: The file is empty");
                }
                else if (memoryStream.Length > sizeLimit)
                {
                    var megabyteSizeLimit = sizeLimit / 1048576;
                    this.logger.LogError("File: The file exceeds {MegabyteSizeLimit} MB", megabyteSizeLimit);
                }
                else if (!IsValidFileExtension(
                             contentDisposition.FileName.Value, memoryStream, 
                             permittedExtensions))
                {
                    this.logger.LogError("File: The file type isn't permitted or the file's " +
                                          "signature doesn't match the file's extension");
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


    private bool IsValidFileExtension(string fileName, Stream data, string[] permittedExtensions)
    {
        if (string.IsNullOrEmpty(fileName) || data == null || data.Length == 0)
        {
            return false;
        }

        var ext = Path.GetExtension(fileName).ToLowerInvariant();

        return !string.IsNullOrEmpty(ext) && permittedExtensions.Contains(ext);
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