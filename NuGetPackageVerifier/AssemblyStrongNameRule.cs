using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NuGet;
using NuGetPackageVerifier.Logging;

namespace NuGetPackageVerifier
{
    public class AssemblyStrongNameRule : IPackageVerifierRule
    {
        public IEnumerable<MyPackageIssue> Validate(IPackageRepository packageRepo, IPackage package, IPackageVerifierLogger logger)
        {
            foreach (IPackageFile currentFile in package.GetFiles())
            {
                string extension = Path.GetExtension(currentFile.Path);
                if (extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    string assemblyPath = Path.ChangeExtension(Path.Combine(Path.GetTempPath(), Path.GetTempFileName()), extension);
                    var isManagedCode = false;
                    var isStrongNameSigned = false;
                    int hresult = 0;
                    try
                    {
                        using (Stream packageFileStream = currentFile.GetStream())
                        {
                            var _assemblyBytes = new byte[packageFileStream.Length];
                            packageFileStream.Read(_assemblyBytes, 0, _assemblyBytes.Length);

                            using (var fileStream = new FileStream(assemblyPath, FileMode.Create))
                            {
                                packageFileStream.Seek(0, SeekOrigin.Begin);
                                packageFileStream.CopyTo(fileStream);
                                fileStream.Flush(true);
                            }

                            if (IsAssemblyManaged(assemblyPath))
                            {
                                isManagedCode = true;
                                var clrStrongName = (IClrStrongName)RuntimeEnvironment.GetRuntimeInterfaceAsObject(new Guid("B79B0ACD-F5CD-409b-B5A5-A16244610B92"), new Guid("9FD93CCF-3280-4391-B3A9-96E1CDE77C8D"));
                                bool verificationForced;
                                hresult = clrStrongName.StrongNameSignatureVerificationEx(assemblyPath, true, out verificationForced);
                                if (hresult == 0)
                                {
                                    isStrongNameSigned = true;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError("Error while verifying strong name signature for {0}: {1}", currentFile.Path, ex.Message);
                    }
                    finally
                    {
                        if (File.Exists(assemblyPath))
                        {
                            File.Delete(assemblyPath);
                        }
                    }
                    if (isManagedCode && !isStrongNameSigned)
                    {
                        yield return PackageIssueFactory.AssemblyNotStrongNameSigned(currentFile.Path, hresult);
                    }
                }
            }
            yield break;
        }

        private static bool IsAssemblyManaged(string assemblyPath)
        {
            // From http://msdn.microsoft.com/en-us/library/ms173100.aspx
            try
            {
                var testAssembly = AssemblyName.GetAssemblyName(assemblyPath);
                return true;
            }
            catch (FileNotFoundException)
            {
                // The file cannot be found
            }
            catch (BadImageFormatException)
            {
                // The file is not an assembly
            }
            catch (FileLoadException)
            {
                // The assembly has already been loaded
            }
            return false;
        }

        [ComConversionLoss, Guid("9FD93CCF-3280-4391-B3A9-96E1CDE77C8D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [ComImport]
        internal interface IClrStrongName
        {
            [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.InternalCall)]
            int GetHashFromAssemblyFile([MarshalAs(UnmanagedType.LPStr)] [In] string pszFilePath, [MarshalAs(UnmanagedType.U4)] [In] [Out] ref int piHashAlg, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] [Out] byte[] pbHash, [MarshalAs(UnmanagedType.U4)] [In] int cchHash, [MarshalAs(UnmanagedType.U4)] out int pchHash);

            [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.InternalCall)]
            int GetHashFromAssemblyFileW([MarshalAs(UnmanagedType.LPWStr)] [In] string pwzFilePath, [MarshalAs(UnmanagedType.U4)] [In] [Out] ref int piHashAlg, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] [Out] byte[] pbHash, [MarshalAs(UnmanagedType.U4)] [In] int cchHash, [MarshalAs(UnmanagedType.U4)] out int pchHash);

            [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.InternalCall)]
            int GetHashFromBlob([In] IntPtr pbBlob, [MarshalAs(UnmanagedType.U4)] [In] int cchBlob, [MarshalAs(UnmanagedType.U4)] [In] [Out] ref int piHashAlg, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] [Out] byte[] pbHash, [MarshalAs(UnmanagedType.U4)] [In] int cchHash, [MarshalAs(UnmanagedType.U4)] out int pchHash);

            [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.InternalCall)]
            int GetHashFromFile([MarshalAs(UnmanagedType.LPStr)] [In] string pszFilePath, [MarshalAs(UnmanagedType.U4)] [In] [Out] ref int piHashAlg, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] [Out] byte[] pbHash, [MarshalAs(UnmanagedType.U4)] [In] int cchHash, [MarshalAs(UnmanagedType.U4)] out int pchHash);

            [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.InternalCall)]
            int GetHashFromFileW([MarshalAs(UnmanagedType.LPWStr)] [In] string pwzFilePath, [MarshalAs(UnmanagedType.U4)] [In] [Out] ref int piHashAlg, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] [Out] byte[] pbHash, [MarshalAs(UnmanagedType.U4)] [In] int cchHash, [MarshalAs(UnmanagedType.U4)] out int pchHash);

            [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.InternalCall)]
            int GetHashFromHandle([In] IntPtr hFile, [MarshalAs(UnmanagedType.U4)] [In] [Out] ref int piHashAlg, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] [Out] byte[] pbHash, [MarshalAs(UnmanagedType.U4)] [In] int cchHash, [MarshalAs(UnmanagedType.U4)] out int pchHash);

            [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.InternalCall)]
            [return: MarshalAs(UnmanagedType.U4)]
            int StrongNameCompareAssemblies([MarshalAs(UnmanagedType.LPWStr)] [In] string pwzAssembly1, [MarshalAs(UnmanagedType.LPWStr)] [In] string pwzAssembly2, [MarshalAs(UnmanagedType.U4)] out int dwResult);

            [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.InternalCall)]
            int StrongNameFreeBuffer([In] IntPtr pbMemory);

            [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.InternalCall)]
            int StrongNameGetBlob([MarshalAs(UnmanagedType.LPWStr)] [In] string pwzFilePath, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] [Out] byte[] pbBlob, [MarshalAs(UnmanagedType.U4)] [In] [Out] ref int pcbBlob);

            [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.InternalCall)]
            int StrongNameGetBlobFromImage([In] IntPtr pbBase, [MarshalAs(UnmanagedType.U4)] [In] int dwLength, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] [Out] byte[] pbBlob, [MarshalAs(UnmanagedType.U4)] [In] [Out] ref int pcbBlob);

            [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.InternalCall)]
            int StrongNameGetPublicKey([MarshalAs(UnmanagedType.LPWStr)] [In] string pwzKeyContainer, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] [In] byte[] pbKeyBlob, [MarshalAs(UnmanagedType.U4)] [In] int cbKeyBlob, out IntPtr ppbPublicKeyBlob, [MarshalAs(UnmanagedType.U4)] out int pcbPublicKeyBlob);

            [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.InternalCall)]
            [return: MarshalAs(UnmanagedType.U4)]
            int StrongNameHashSize([MarshalAs(UnmanagedType.U4)] [In] int ulHashAlg, [MarshalAs(UnmanagedType.U4)] out int cbSize);

            [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.InternalCall)]
            int StrongNameKeyDelete([MarshalAs(UnmanagedType.LPWStr)] [In] string pwzKeyContainer);

            [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.InternalCall)]
            int StrongNameKeyGen([MarshalAs(UnmanagedType.LPWStr)] [In] string pwzKeyContainer, [MarshalAs(UnmanagedType.U4)] [In] int dwFlags, out IntPtr ppbKeyBlob, [MarshalAs(UnmanagedType.U4)] out int pcbKeyBlob);

            [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.InternalCall)]
            int StrongNameKeyGenEx([MarshalAs(UnmanagedType.LPWStr)] [In] string pwzKeyContainer, [MarshalAs(UnmanagedType.U4)] [In] int dwFlags, [MarshalAs(UnmanagedType.U4)] [In] int dwKeySize, out IntPtr ppbKeyBlob, [MarshalAs(UnmanagedType.U4)] out int pcbKeyBlob);

            [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.InternalCall)]
            int StrongNameKeyInstall([MarshalAs(UnmanagedType.LPWStr)] [In] string pwzKeyContainer, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] [In] byte[] pbKeyBlob, [MarshalAs(UnmanagedType.U4)] [In] int cbKeyBlob);

            [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.InternalCall)]
            int StrongNameSignatureGeneration([MarshalAs(UnmanagedType.LPWStr)] [In] string pwzFilePath, [MarshalAs(UnmanagedType.LPWStr)] [In] string pwzKeyContainer, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] [In] byte[] pbKeyBlob, [MarshalAs(UnmanagedType.U4)] [In] int cbKeyBlob, [In] [Out] IntPtr ppbSignatureBlob, [MarshalAs(UnmanagedType.U4)] out int pcbSignatureBlob);

            [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.InternalCall)]
            int StrongNameSignatureGenerationEx([MarshalAs(UnmanagedType.LPWStr)] [In] string wszFilePath, [MarshalAs(UnmanagedType.LPWStr)] [In] string wszKeyContainer, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] [In] byte[] pbKeyBlob, [MarshalAs(UnmanagedType.U4)] [In] int cbKeyBlob, [In] [Out] IntPtr ppbSignatureBlob, [MarshalAs(UnmanagedType.U4)] out int pcbSignatureBlob, [MarshalAs(UnmanagedType.U4)] [In] int dwFlags);

            [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.InternalCall)]
            int StrongNameSignatureSize([MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] [In] byte[] pbPublicKeyBlob, [MarshalAs(UnmanagedType.U4)] [In] int cbPublicKeyBlob, [MarshalAs(UnmanagedType.U4)] out int pcbSize);

            [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.InternalCall)]
            [return: MarshalAs(UnmanagedType.U4)]
            int StrongNameSignatureVerification([MarshalAs(UnmanagedType.LPWStr)] [In] string pwzFilePath, [MarshalAs(UnmanagedType.U4)] [In] int dwInFlags, [MarshalAs(UnmanagedType.U4)] out int dwOutFlags);

            [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.InternalCall)]
            [return: MarshalAs(UnmanagedType.U4)]
            int StrongNameSignatureVerificationEx([MarshalAs(UnmanagedType.LPWStr)] [In] string pwzFilePath, [MarshalAs(UnmanagedType.I1)] [In] bool fForceVerification, [MarshalAs(UnmanagedType.I1)] out bool fWasVerified);

            [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.InternalCall)]
            [return: MarshalAs(UnmanagedType.U4)]
            int StrongNameSignatureVerificationFromImage([In] IntPtr pbBase, [MarshalAs(UnmanagedType.U4)] [In] int dwLength, [MarshalAs(UnmanagedType.U4)] [In] int dwInFlags, [MarshalAs(UnmanagedType.U4)] out int dwOutFlags);

            [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.InternalCall)]
            int StrongNameTokenFromAssembly([MarshalAs(UnmanagedType.LPWStr)] [In] string pwzFilePath, out IntPtr ppbStrongNameToken, [MarshalAs(UnmanagedType.U4)] out int pcbStrongNameToken);

            [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.InternalCall)]
            int StrongNameTokenFromAssemblyEx([MarshalAs(UnmanagedType.LPWStr)] [In] string pwzFilePath, out IntPtr ppbStrongNameToken, [MarshalAs(UnmanagedType.U4)] out int pcbStrongNameToken, out IntPtr ppbPublicKeyBlob, [MarshalAs(UnmanagedType.U4)] out int pcbPublicKeyBlob);

            [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.InternalCall)]
            int StrongNameTokenFromPublicKey([MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] [In] byte[] pbPublicKeyBlob, [MarshalAs(UnmanagedType.U4)] [In] int cbPublicKeyBlob, out IntPtr ppbStrongNameToken, [MarshalAs(UnmanagedType.U4)] out int pcbStrongNameToken);
        }
    }
}
