
namespace EntityParser
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string describeFilePath = @"C:\Users\yingfand\OneDrive - Microsoft\Temp\UCM\Xandr\Response\account-describe.json";
            string sampleFilePath = @"C:\Users\yingfand\OneDrive - Microsoft\Temp\UCM\Xandr\Response\account-sample.json";
            string outPutFilePath = @"C:\Users\yingfand\Download\Ouput";
            string entityName = "Account";
            var parser = new Parser(describeFilePath, sampleFilePath, outPutFilePath, entityName);
            parser.Process();
        }
    }
}
