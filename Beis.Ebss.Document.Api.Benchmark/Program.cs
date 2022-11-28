using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using nClam;

namespace Beis.Ebss.Document.Api.Benchmark
{
    public class Md5VsSha256
    {
        [Benchmark]
        public async Task<ClamScanResult> PdfImportPerformance()
        {
            var fileBytes = await File.ReadAllBytesAsync("/users/will/Desktop/5315.pdf");
            var clam = new ClamClient("localhost", 3310);
            var scanResult = await clam.SendAndScanFileAsync(fileBytes);
            Console.WriteLine("Scan result: " + scanResult.RawResult);
            return scanResult;
        }
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run(typeof(Program).Assembly);
        }
    }
}