using System;

namespace Tests.ComputeSystem
{
    public class TestRunner
    {
        public static void Main()
        {
            try
            {
                // Run AttributeMap tests
                AttributeMapTests.RunTests();
                
                // Add more test classes here as they are created
                // Example: OtherComponentTests.RunTests();
                
                Console.WriteLine("All tests completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Test failed with error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
            
            // Only wait for key press if running in an interactive console
            if (Environment.UserInteractive && !Console.IsInputRedirected)
            {
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey();
            }
        }
    }
}
