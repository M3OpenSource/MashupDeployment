/*
    Thibaud Lopez Schneider, Ciber
    2016-10-04
    V3: Add code to generate the .lawsonapp in case it's needed to deploy to LCM
    V2: Added this comments section; moved usage to: if args.Length==0; added Mashup version number to _.mashup filename; corrected profile message to say "this" tool
    V1: https://m3ideas.org/2016/06/07/continuous-integration-of-mashups-4-command-line/
*/
using System;
using System.IO;
using System.Reflection;
using Mango.UI.Services.Mashup;
using Mango.UI.Services.Mashup.Internal;
using MangoServer.mangows;
using Mashup.Designer;
using System.Xml;

namespace Mashups
{
    class Deploy
    {
        static void Main(string[] args)
        {
            // check the arguments
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: Deploy.exe Mashup1,Mashup2,Mashup3 DEV,TST");
                return;
            }

            // get the M3 userid/password
            Console.Write("Userid: ");
            string userid = Console.ReadLine();
            Console.Write("Password: ");
            string password = Console.ReadLine();

            // for each directory
            string[] directories = args[0].Split(',');
            foreach (string directory in directories)
            {
                // for each Mashup
                string[] files = Directory.GetFiles(directory, "*.manifest", SearchOption.AllDirectories);
                foreach (string file in files)
                {
                    Console.WriteLine("Opening {0}", file);
                    ManifestDesigner manifest = new ManifestDesigner(new FileInfo(file));
                    string name = manifest.DeploymentName + Defines.ExtensionMashup;
                    string nameAndVersion = manifest.DeploymentName + "_" + manifest.Version + Defines.ExtensionMashup;
                    Console.WriteLine("Generating {0}", nameAndVersion);
                    // generate the Mashup package
                    if (SaveMashupFile(manifest))
                    {
                        // get the resulting binary contents
                        byte[] bytes = File.ReadAllBytes(manifest.File.Directory + "\\" + nameAndVersion);
                        // for each M3 environment
                        string[] environments = args[1].Split(',');
                        foreach (string environment in environments)
                        {
                            // deploy
                            Console.WriteLine("Deploying {0} to {1}", name, environment);
                            DeployMashup(name, bytes, environment, userid, password);
                        }
                        Console.WriteLine("DONE");
                    }
                    // generate the LawsonApp for LCM
                    string package = manifest.File.Directory + "\\" + manifest.DeploymentName + "_" + manifest.Version + Mango.Core.ApplicationConstants.FileExtension;
                    Console.WriteLine("Generating {0}", package);
                    XmlDocument appConfigDoc = (XmlDocument) typeof(DeploymentHelper).InvokeMember("CreateAppConfig", BindingFlags.InvokeMethod | BindingFlags.NonPublic | BindingFlags.Static, null, null, new Object[] { manifest, PackageHelper.DeployTarget_LSO });
                    Stream stream = new FileInfo(package).OpenWrite();
                    FileStream appStream = (FileStream)typeof(DeploymentHelper).InvokeMember("CreateAppStream", BindingFlags.InvokeMethod | BindingFlags.NonPublic | BindingFlags.Static, null, null, new Object[] { manifest, appConfigDoc, PackageHelper.DeployTarget_LSO });
                    appStream.CopyTo(stream);
                    appStream.Close();
                    stream.Close();
                }
            }
        }
  
        /*
            Create the Mashup package (*.mashup) from the specified Mashup Manifest.
            Inspired by Mashup.Designer.DeploymentHelper.SaveMashupFile
        */
        static bool SaveMashupFile(ManifestDesigner manifest)
        {
            try
            {
                // validate Manifest
                typeof(DeploymentHelper).InvokeMember("ValidateManifest", BindingFlags.InvokeMethod | BindingFlags.NonPublic | BindingFlags.Static, null, null, new Object[] { manifest });
                // create Mashup package
                string defaultFileName = manifest.DeploymentName + "_" + manifest.Version + Defines.ExtensionMashup;
                FileInfo packageFile = new FileInfo(manifest.File.Directory + "\\" + defaultFileName);
                PackageHelper.CreatePackageFromManifest(manifest, manifest.File, packageFile, PackageHelper.DeployTarget_LSO);
                // check Mashup profile
                if (manifest.GetProfileNode() != null)
                {
                    string message = "Please note that this Mashup contains a profile section and should be deployed using Life Cycle Manager. Warning - Using this tool will not result in a merge of profile information into the profile.";
                    Console.WriteLine(message);
                }
                return true;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return false;
            }
        }

        /*
            Deploy the specified Mashup name and file (*.mashup binary contents) to the specified M3 environment (see App.config) authenticating with the specified M3 userid and password.
            Inspired by /mangows/InstallationPointManager.wsdl/DeployMashup
        */
        static void DeployMashup(string Name, byte[] MashupFile, string environment, string userid, string password)
        {
            InstallationPointManagerClient client = new MangoServer.mangows.InstallationPointManagerClient(environment);
            client.ClientCredentials.UserName.UserName = userid;
            client.ClientCredentials.UserName.Password = password;
            client.DeployMashup(Name, MashupFile);
        }

    }
}
