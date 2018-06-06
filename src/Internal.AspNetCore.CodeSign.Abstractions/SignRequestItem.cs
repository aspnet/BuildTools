// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.AspNetCore.BuildTools.CodeSign
{
    /// <summary>
    /// Represents a file that should be signed.
    /// </summary>
    public class SignRequestItem
    {
        private static readonly Dictionary<string, SignRequestItemType> FileExtensionMapping =
            new Dictionary<string, SignRequestItemType>(StringComparer.OrdinalIgnoreCase)
            {
                ["zip"] = SignRequestItemType.Zip,
                ["mpack"] = SignRequestItemType.Zip,
                ["vsix"] = SignRequestItemType.Vsix,
                ["nupkg"] = SignRequestItemType.Nupkg,
                ["dll"] = SignRequestItemType.File,
                ["exe"] = SignRequestItemType.File,
                ["ps1"] = SignRequestItemType.File,
                ["psd1"] = SignRequestItemType.File,
                ["psm1"] = SignRequestItemType.File,
                ["psc1"] = SignRequestItemType.File,
                ["ps1xml"] = SignRequestItemType.File,
            };

        private readonly SignRequestCollection _children = new SignRequestCollection();

        /// <summary>
        /// Create an exclusion to signing.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static SignRequestItem CreateExclusion(string path)
            => new SignRequestItem(path, SignRequestItemType.Exclusion);

        /// <summary>
        /// A zip archive file.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static SignRequestItem CreateZip(string path)
            => new SignRequestItem(path, SignRequestItemType.Zip);

        /// <summary>
        /// A binary (.exe, .dll) to be signed.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="certificateName"></param>
        /// <param name="strongName"></param>
        /// <returns></returns>
        public static SignRequestItem CreateFile(string path, string certificateName, string strongName)
            => new SignRequestItem(path, SignRequestItemType.File)
            {
                Certificate =  certificateName,
                StrongName = strongName,
            };

        /// <summary>
        /// A .nupkg to be signed.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="certificateName"></param>
        /// <returns></returns>
        public static SignRequestItem CreateNugetPackage(string path, string certificateName)
            => new SignRequestItem(path, SignRequestItemType.Nupkg)
            {
                Certificate =  certificateName,
            };

        /// <summary>
        /// A .vsix to be signed.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="certificateName"></param>
        /// <returns></returns>
        public static SignRequestItem CreateVsix(string path, string certificateName)
            => new SignRequestItem(path, SignRequestItemType.Vsix)
            {
                Certificate =  certificateName,
            };

        /// <summary>
        /// Determines if a file is signable based on the file extension
        /// </summary>
        /// <param name="filePath">The file extension (beginning with a dot). <seealso cref="System.IO.Path.GetExtension"/></param>
        /// <returns>True if the path can be code-signed.</returns>
        public static bool IsFileTypeSignable(string filePath)
            => GetTypeFromFileExtension(System.IO.Path.GetExtension(filePath).TrimStart('.')) != SignRequestItemType.Unknown;

        /// <summary>
        /// Get an item type based on its file extension.
        /// </summary>
        /// <param name="fileExtension"></param>
        /// <returns></returns>
        public static SignRequestItemType GetTypeFromFileExtension(string fileExtension)
        {
            if (FileExtensionMapping.TryGetValue(fileExtension.TrimStart('.'), out var type))
            {
                return type;
            }

            return SignRequestItemType.Unknown;
        }

        /// <summary>
        /// Initialize an instance of <see cref="SignRequestItem" />
        /// </summary>
        /// <param name="path"></param>
        /// <param name="itemType"></param>
        internal SignRequestItem(string path, SignRequestItemType itemType)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
            ItemType = itemType;
        }

        /// <summary>
        /// The file path. Should be relative to the manifest or container.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// The type of the item.
        /// </summary>
        public SignRequestItemType ItemType { get; }

        /// <summary>
        /// The name of the code signing certificate to use.
        /// </summary>
        public string Certificate { get; internal set; }

        /// <summary>
        /// The name of the strong name key to use (for .NET assemblies only).
        /// </summary>
        public string StrongName { get; internal set; }

        /// <summary>
        /// Excluded from code signing.
        /// </summary>
        public bool Excluded => ItemType == SignRequestItemType.Exclusion;

        /// <summary>
        /// True for item types which can contain children, such as .zip or .nupkg files.
        /// </summary>
        public bool CanContainChildren
            => ItemType == SignRequestItemType.Nupkg
               || ItemType == SignRequestItemType.Zip
               || ItemType == SignRequestItemType.Vsix;

        /// <summary>
        /// Children of the sign request item.
        /// </summary>
        public IEnumerable<SignRequestItem> Children => _children;

        /// <summary>
        /// Add a child type.
        /// </summary>
        /// <param name="item"></param>
        public void AddChild(SignRequestItem item)
        {
            if (!CanContainChildren)
            {
                throw new InvalidOperationException("Cannot add a child to items which do not allow nested files");
            }

            _children.Add(item);
        }
    }
}
