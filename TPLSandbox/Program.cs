//--------------------------------------------------------------------------
// 
//  Copyright (c) Microsoft Corporation.  All rights reserved. 
// 
//  File: Program.cs
//
//--------------------------------------------------------------------------

using System;
using System.Security;
using System.Security.Permissions;
using System.Security.Policy;
using System.Reflection;
using System.Threading.Tasks;

namespace TPLSandbox
{
    class Program
    {
        static void Main(string[] args)
        {
            // Create a PermissionSet for the sandbox AppDomain (that will actually let it do something)
            PermissionSet permSet = new PermissionSet(PermissionState.None);
            permSet.AddPermission(new SecurityPermission(SecurityPermissionFlag.Execution));
            permSet.AddPermission(new FileIOPermission(FileIOPermissionAccess.Read, System.IO.Path.Combine(Environment.CurrentDirectory, "BuggyPlugin.exe")));
            permSet.AddPermission(new FileIOPermission(FileIOPermissionAccess.PathDiscovery, Environment.CurrentDirectory));
            permSet.AddPermission(new UIPermission(UIPermissionWindow.AllWindows, UIPermissionClipboard.AllClipboard));

            // Create an AppDomainSetup. This is where we can specify an AppDomainInitializer delegate 
            // that will execute within the current assembly (which is full-trust).
            AppDomainSetup setup = new AppDomainSetup();
            setup.ApplicationBase = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
            setup.AppDomainInitializer = _ =>
            {
                // Subscribe to the TPL exception event. Unobserved Task exceptions will be logged 
                // and marked as "observed" (suppressing the usual exception escalation behavior
                // which would crash the process).
                TaskScheduler.UnobservedTaskException += (object sender, UnobservedTaskExceptionEventArgs exceptionArgs) =>
                {
                    LogError(exceptionArgs.Exception);
                    exceptionArgs.SetObserved();
                };
            };

            // Create the sandbox AppDomain
            AppDomain sandbox = AppDomain.CreateDomain(
                "Sandbox",
                null,
                setup,
                permSet,
                CreateStrongName(Assembly.GetExecutingAssembly()));

            // Execute the BuggyPlugin assembly in the sandbox AppDomain.
            // The try/catch block will handle any synchronous exceptions.
            try
            {
                sandbox.ExecuteAssembly("BuggyPlugin.exe");
            }
            catch (Exception e)
            {
                LogError(e);
            }

            // Uncommenting the following statement causes the application to crash,
            //     proving that unobserved Task exceptions here in the host AppDomain
            //     are treated normally (as serious errors).
            //SimulateError();

            // Force a garbage collection to reveal any unobserved Task exceptions
            GC.Collect();

            AppDomain.Unload(sandbox);
        }

        /// <summary>
        /// Logs an exception (prints it to the console)
        /// </summary>
        /// <param name="e">The error.</param>
        public static void LogError(Exception e)
        {
            Console.WriteLine("****\n" + e.Message + "\n****");
        }

        /// <summary>
        /// Simulates an unhandled Task exception.
        /// </summary>
        public static void SimulateError()
        {
            Task t = Task.Factory.StartNew(() =>
            {
                throw new Exception("serious error.");
            });

            // Wait on Task 't' without marking its exceptions as observed
            ((IAsyncResult)t).AsyncWaitHandle.WaitOne();
        }

        /// <summary>
        /// Create a StrongName that matches a specific assembly
        /// </summary>
        /// See: http://blogs.msdn.com/shawnfa/archive/2005/08/08/449050.aspx
        public static StrongName CreateStrongName(Assembly assembly)
        {
            if (assembly == null)
                throw new ArgumentNullException("assembly");

            AssemblyName assemblyName = assembly.GetName();
            if (assemblyName == null)
                throw new ArgumentException("Could not get assembly name");

            // get the public key blob
            byte[] publicKey = assemblyName.GetPublicKey();
            if (publicKey == null || publicKey.Length == 0)
                throw new InvalidOperationException("Assembly is not strongly named");

            StrongNamePublicKeyBlob keyBlob = new StrongNamePublicKeyBlob(publicKey);

            // and create the StrongName
            return new StrongName(keyBlob, assemblyName.Name, assemblyName.Version);
        }
    }
}
