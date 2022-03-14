/*
 * Copyright (c) The LumiaWOA and DuoWOA authors
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
using CommandLine;

namespace DriverUpdater
{
    [Verb("update-drivers", isDefault: true, HelpText = "Updates the drivers on a target phone device.")]
    internal class CLIOptions
    {
        [Option('d', "definition-file", HelpText = "The path to the definition file to use.", Required = true)]
        public string DefinitionFile { get; set; }

        [Option('r', "repository-path", HelpText = "The path to the driver repository.", Required = true)]
        public string RepositoryPath { get; set; }

        [Option('p', "phone-path", HelpText = "The path to the phone's Windows Installation.", Required = true)]
        public string PhonePath { get; set; }

        [Option('a', "is-arm32", HelpText = "Indicates the target runs an ARM32 Windows Operating System (EOL)", Required = false, Default = false)]
        public bool IsARM { get; set; }

        [Option('n', "no-integratepostupgrade", HelpText = "Indicates to not provision the target device for post upgrade tasks", Required = false, Default = false)]
        public bool NoIntegratePostUpgrade { get; set; }
    }
}
