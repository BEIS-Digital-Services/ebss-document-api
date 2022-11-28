namespace Beis.Ebss.Document.Api.Options;

public class DocumentOptions
{
    public const string Section = "Document";
    
    public string LocalStoragePath { get; set; }
 
    public int MaxFileSize { get; set; }

    public string[] AcceptableFileExtensions { get; set; }
}