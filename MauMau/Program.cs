using System;
using LamestWebserver;
using LamestWebserver.Core;

namespace Demos
{
    class Program
    {
        /// <summary>
        /// This is the starting point for this program
        /// </summary>
        /// <param name="args">the arguments the executable has started with</param>
        static void Main(string[] args)
        {
            // Configures the Logger to write all information to the console and to a file ('lws.log' by default).
            Logger.OutputSource = Logger.EOutputSource.Console | Logger.EOutputSource.File;

            try
            {
                // Create a new Webserver at port 8080 with the data location "./web" - the web folder of this Project
                // Make sure all contents of your data location folder are copied into the build directory on compile so the webserver can find them at runtime.
                //
                // You'll probably be using port 80 / 443 later, because that's the default http / https port - but for now let's just use port 8080, because on some machines port 80 is already in use.
                using (var webserver = new WebServer(8080, "./web"))
                {
                    // Automatically Discovers the Pages in this assembly and registers them at the webserver
                    // Alternatively you might want to call all constructors of the pages manually here.
                    // 
                    // Pages inheriting from IURLIdentifyable (like PageResponse, DirectoryResponse, ElementResponse, etc.) 
                    //  with an empty constructor (see i.e. MainPage.cs) are automatically added when calling Master.DiscoverPages();
                    //  if you don't want such a constructor to be called mark the containing class with the LamestWebserver.Attributes.IgnoreDiscovery Attribute.
                    Master.DiscoverPages();

                    // Open a browser window at the base-URL of our webserver.
                    //System.Diagnostics.Process.Start($"http://localhost:{webserver.Port}/");

#if DEBUG
                    // Add a Server Instance to view the LamestWebserver DebugView with. This Webserver will run on a different port (port 8081 in this case), 
                    //  so you can just switch to a different port in your browser. You should only use the DebugView for Debugging Purposes. 
                    //  In the final product (or in Release builds) just don't start it, if you don't want everyone to be able to see your internal debugging Data.
                    WebServer debugViewWebserver = new WebServer(8081, LamestWebserver.RequestHandlers.DebugView.DebugResponse.DebugViewRequestHandler);

                    // Add the debugViewWebserver to our main Webserver so that it'll be closed whenever our main one is closed.
                    webserver.AddDependentWebsever(debugViewWebserver);
#endif

                    Console.WriteLine("MauMau Server.\n\nType 'exit' to quit.");

                    // Keep the Server available until we enter exit.
                    while (Console.ReadLine() != "exit") { }
                }
            }
            // When the tcp port (8080 / 8081 in this example) is already used by another application an exception is thrown.
            catch (Exception e)
            {
                // Display the message of the exception so you can read it and wait until a button is pressed, so you have enough time to read it.
                Console.WriteLine(e.Message);

                Console.WriteLine("\nPress any key to quit...");
                Console.ReadKey();
            }
        }
    }
}
