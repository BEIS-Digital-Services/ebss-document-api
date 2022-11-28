namespace Beis.Ebss.Document.Api.Options;

public class ClamAVOptions
{
    public const string Section = "ClamAVServer";
    
    public string Url { get; set; }
 
    public int Port { get; set; }
}