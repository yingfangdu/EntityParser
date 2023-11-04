
namespace EntityParser
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string describeFilePath = @"C:\Users\yingfand\OneDrive - Microsoft\Temp\UCM\Xandr\Response\case-describe.json";
            string sampleFilePath = @"C:\Users\yingfand\OneDrive - Microsoft\Temp\UCM\Xandr\Response\case-sample.json";
            string outPutFilePath = @"C:\Users\yingfand\OneDrive - Microsoft\Temp\UCM\Xandr\Ouput";
            string entityName = "Case";
            var parser = new Parser(describeFilePath, sampleFilePath, outPutFilePath, entityName);
            parser.Process();
        }
    }
}
