using System;
using System.IO;
using dnlib.DotNet;

namespace TaskPubliciser
{
    public class Publicise : Microsoft.Build.Utilities.Task
    {
        private const string suffix = "_publicised";

        public string TargetAssemblyPath { get; set; }
        public string OutputPath { get; set; }
        public string Suffix { get; set; } = suffix;
        public bool Logging { get; set; } = false;

        public override bool Execute()
        {
            if (!ValidityChecks()) return false;

            var asmName = Path.GetFileNameWithoutExtension(TargetAssemblyPath);

            // Get the hash for the file
            var hash = GetHash();

            // Get the possibly not existing hash for the previous iteration
            var oldHash = GetExistingHash(asmName, out var hashPath);

            // No work to do, the file has not been changed, and the dll existS
            if (hash == oldHash)
            {
                if(Logging) Log.LogError("[TaskPubliciser] Hashes are equal, exiting");
                return true;
            }

            // Publicise our Assembly
            PubliciseAssembly(asmName);

            // Update our hash
            File.WriteAllText(hashPath, hash);

            if (Logging) Log.LogError($"[TaskPubliciser] Exiting Execution for Target: {TargetAssemblyPath}\nOutput: {OutputPath}\n Suffix: {Suffix}\n Logging {Logging}");

            return true;
        }

        public string GetHash() // Use the last time the file was written to as the hash
        {
            var lastWriteTime = File.GetLastWriteTime(TargetAssemblyPath);

            if (Logging) Log.LogError($"[TaskPubliciser] Retrieving hash: File was last written: {lastWriteTime.ToString()}\nIn binary: {lastWriteTime.ToBinary()}");
            
            return lastWriteTime.ToBinary().ToString();
        }

        public string GetExistingHash(string asmName, out string hashPath)
        {
            hashPath = Path.Combine(OutputPath, $"{asmName}_hash.hash");

            var hash = File.Exists(hashPath) ? File.ReadAllText(hashPath) : null;

            if (Logging) Log.LogError($"[TaskPubliciser] Retrieving existing hash: File was last written: {hash}");

            return hash;
        }

        public bool ValidityChecks()
        {
            if (TargetAssemblyPath == null) throw new ArgumentNullException("TargetAssemblyPath");
            if (OutputPath == null) throw new ArgumentNullException("OutputPath");

            // Make sure the target assembly actually exists
            if (!File.Exists(TargetAssemblyPath)) return false;

            return true;
        }

        // https://gist.github.com/Zetrith/d86b1d84e993c8117983c09f1a5dcdcd
        public void PubliciseAssembly(string asmName)
        {
            ModuleDef assembly = ModuleDefMD.Load(TargetAssemblyPath);

            foreach (TypeDef type in assembly.GetTypes())
            {
                type.Attributes &= ~TypeAttributes.VisibilityMask;

                if (type.IsNested)
                {
                    type.Attributes |= TypeAttributes.NestedPublic;
                }
                else
                {
                    type.Attributes |= TypeAttributes.Public;
                }

                foreach (MethodDef method in type.Methods)
                {
                    method.Attributes &= ~MethodAttributes.MemberAccessMask;
                    method.Attributes |= MethodAttributes.Public;
                }

                foreach (FieldDef field in type.Fields)
                {
                    field.Attributes &= ~FieldAttributes.FieldAccessMask;
                    field.Attributes |= FieldAttributes.Public;
                }
            }


            if (Logging) Log.LogError($"[TaskPubliciser] Writing to the new publicised assembly");

            assembly.Write($"{OutputPath}{asmName}{Suffix}.dll");
        }
    }
}
