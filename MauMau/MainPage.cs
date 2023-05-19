using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LamestWebserver;
using LamestWebserver.UI;

namespace Demos
{
    /// <summary>
    /// This class inherits from ElementResponse, a prototype for responding UI Elements to the client.
    /// There should be always only one instance of this class.
    /// Whenever an instance is created, the current instance is the one registered as response at the server.
    /// </summary>
    public class MainPage : ElementResponse
    {
        /// <summary>
        /// Register this Page to be the default response of the server - located at the "/" URL
        /// You don't need to call this constructor anywhere if you are using Master.DiscoverPages() or the LamestWebserver Host Service.
        /// If you want to let your constructor be called automatically, please make sure, that it needs no parameters to be called - like this one.
        /// </summary>
        public MainPage() : base("/")
        {
        }

        /// <summary>
        /// This method retrieves the page for the user
        /// </summary>
        /// <param name="sessionData">the sessionData for the current user</param>
        /// <returns>the response</returns>
        protected override HElement GetElement(SessionData sessionData)
        {
            // Create a new Page outline for the browser. 
            var page = new PageBuilder("Main");

            page.AddElement(new HScript(ScriptCollection.GetPageReferalToX, nameof(MauMau)));

            // Return the response.
            return page;
        }

        /// <summary>
        /// Let's just create a prototype of this layout, so we can use it more easily.
        /// Don't worry too much about the `HSelectivelyCacheableElement`.
        /// </summary>
        /// <param name="elements">the elements displayed on the page</param>
        /// <param name="filename">the filename to display</param>
        /// <returns>the page includig all layout elements</returns>
        internal static HSelectivelyCacheableElement GetPage(IEnumerable<HElement> elements, string filename)
        {
            // Create the page
            var page = new PageBuilder("LamestWebserver Reference")
            {
                StylesheetLinks = {"/style.css" } // <- Slash in front of the style.css tells the browser to always look for the files in the top directory
            };

            // Add the main-Container with all the elements and the footer
            page.AddElements(
                new HContainer()
                {
                    Class = "main",
                    Elements = elements.ToList(), 
                    
                    // We'll take a look at what this does in the Caching tutorial.
                    CachingType = LamestWebserver.Caching.ECachingType.Cacheable
                },
                new HContainer()
                {
                    Class = "footer",
                    Elements =
                    {
                        new HImage("/lwsfooter.png"), // <- Slash in front of the lwsfooter.png tells the browser to always look for the files in the top directory
                        new HText(filename + "\nLamestWebserver Reference v" + typeof(MainPage).Assembly.GetName().Version)
                    },

                    CachingType = LamestWebserver.Caching.ECachingType.Cacheable
                });

            return page;
        }
    }
}
