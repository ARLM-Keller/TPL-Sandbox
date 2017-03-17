//--------------------------------------------------------------------------
// 
//  Copyright (c) Microsoft Corporation.  All rights reserved. 
// 
//  File: BuggyPlugin.cs
//
//--------------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace BuggyPlugin
{
    class BuggyPlugin
    {
        static void Main(string[] args)
        {
            Task t = Task.Factory.StartNew(() =>
            {
                throw new Exception("oops");
            });

            // Wait on Task 't' without observing its exception(s)
            ((IAsyncResult)t).AsyncWaitHandle.WaitOne();
        }
    }
}
