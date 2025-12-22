using System;
using System.Collections.Generic;
using System.Text;

namespace Transcriber.Core
{
    internal class main
    {
        static async Task Main(string[] args)
        {
            var test = new TestKBLibTranscription();
            await test.RunTest();
        }
    }
}
