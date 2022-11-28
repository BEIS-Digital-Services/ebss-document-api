using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Beis.Ebss.Document.Api.Filters;

public class SwaggerFileOperationFilter : IOperationFilter  
{  
    public void Apply(OpenApiOperation operation, OperationFilterContext context)  
    {

        var fileUploadMime = "multipart/form-data";  
        if (operation.RequestBody == null 
            || !operation.RequestBody.Content.Any(x => x.Key.Equals(fileUploadMime, StringComparison.InvariantCultureIgnoreCase)))  
            return;  
  
        var fileParams = context.MethodInfo.GetParameters().Where(p => p.ParameterType == typeof(IFormFile));  
        operation.RequestBody.Content[fileUploadMime].Schema.Properties =  
            fileParams.ToDictionary(k => k.Name, v => new OpenApiSchema()  
            {  
                Type = "string",  
                Format = "binary"  
            });  
    }  
}  

// try
// {
//     // scan for virus...
//
//     this._logger.LogInformation("ClamAV scan begin for file {0}",
//         trustedFileNameForFileStorage);
//     var clam = new ClamClient(this._ClamAvOptions.Url,
//         Convert.ToInt32(this._ClamAvOptions.Port));
//     var scanResult = await clam.SendAndScanFileAsync(streamedFileContent);
//
//     switch (scanResult.Result)
//     {
//         case ClamScanResults.Clean:
//             this._logger.LogInformation("The file is clean! ScanResult:{1}",
//                 scanResult.RawResult);
//             break;
//         case ClamScanResults.VirusDetected:
//             this._logger.LogError("Virus Found! Virus name: {1}",
//                 scanResult.InfectedFiles.FirstOrDefault().VirusName);
//             break;
//         case ClamScanResults.Error:
//             this._logger.LogError("An error occured while scaning the file! ScanResult: {1}",
//                 scanResult.RawResult);
//             break;
//         case ClamScanResults.Unknown:
//             this._logger.LogError("Unknown scan result while scaning the file! ScanResult: {0}",
//                 scanResult.RawResult);
//             break;
//     }
// }
// catch (Exception ex)
// {
//
//     this._logger.LogError("ClamAV Scan Exception: {0}", ex.ToString());
// }