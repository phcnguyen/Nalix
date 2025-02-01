﻿using Notio.Network.Web.Files;
using Notio.Network.Web.Utilities;
using System;
using System.IO;
using System.Reflection;

namespace Notio.Network.Web.WebModule;

public static partial class WebModuleContainerExtensions
{
    /// <summary>
    /// Creates an instance of <see cref="FileSystemProvider"/>, uses it to initialize
    /// a <seealso cref="FileModule"/>, and adds the latter to a module container.
    /// </summary>
    /// <typeparam name="TContainer">The type of the module container.</typeparam>
    /// <param name="this">The <typeparamref name="TContainer"/> on which this method is called.</param>
    /// <param name="baseRoute">The base route of the module.</param>
    /// <param name="fileSystemPath">The path of the directory to serve.</param>
    /// <param name="isImmutable"><see langword="true"/> if files and directories in
    /// <paramref name="fileSystemPath"/> are not expected to change during a web server's
    /// lifetime; <see langword="false"/> otherwise.</param>
    /// <param name="configure">A callback used to configure the module.</param>
    /// <returns><paramref name="this"/> with a <see cref="FileModule"/> added.</returns>
    /// <exception cref="NullReferenceException"><paramref name="this"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="fileSystemPath"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="fileSystemPath"/> is not a valid local path.</exception>
    /// <seealso cref="FileModule"/>
    /// <seealso cref="FileSystemProvider"/>
    /// <seealso cref="IWebModuleContainer.Modules"/>
    /// <seealso cref="IComponentCollection{T}.Add"/>
    public static TContainer WithStaticFolder<TContainer>(
        this TContainer @this,
        string baseRoute,
        string fileSystemPath,
        bool isImmutable,
        Action<FileModule>? configure = null)
        where TContainer : class, IWebModuleContainer
    {
        return WithStaticFolder(@this, null, baseRoute, fileSystemPath, isImmutable, configure);
    }

    /// <summary>
    /// Creates an instance of <see cref="FileSystemProvider"/>, uses it to initialize
    /// a <seealso cref="FileModule"/>, and adds the latter to a module container,
    /// giving it the specified <paramref name="name"/> if not <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// OSX doesn't support <see cref="FileSystemWatcher" />, the parameter <paramref name="isImmutable" /> will be always <see langword="true"/>.
    /// </remarks>
    /// <typeparam name="TContainer">The type of the module container.</typeparam>
    /// <param name="this">The <typeparamref name="TContainer"/> on which this method is called.</param>
    /// <param name="name">The name.</param>
    /// <param name="baseRoute">The base route of the module.</param>
    /// <param name="fileSystemPath">The path of the directory to serve.</param>
    /// <param name="isImmutable"><see langword="true"/> if files and directories in
    /// <paramref name="fileSystemPath"/> are not expected to change during a web server's
    /// lifetime; <see langword="false"/> otherwise.</param>
    /// <param name="configure">A callback used to configure the module.</param>
    /// <returns><paramref name="this"/> with a <see cref="FileModule"/> added.</returns>
    /// <exception cref="NullReferenceException"><paramref name="this"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="fileSystemPath"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="fileSystemPath"/> is not a valid local path.</exception>
    /// <seealso cref="FileModule"/>
    /// <seealso cref="FileSystemProvider"/>
    /// <seealso cref="IWebModuleContainer.Modules"/>
    /// <seealso cref="IComponentCollection{T}.Add"/>
    public static TContainer WithStaticFolder<TContainer>(
        this TContainer @this,
        string? name,
        string baseRoute,
        string fileSystemPath,
        bool isImmutable,
        Action<FileModule>? configure = null)
        where TContainer : class, IWebModuleContainer
    {
        FileSystemProvider provider = new(fileSystemPath, isImmutable);
        try
        {
            FileModule module = new(baseRoute, provider);
            return WithModule(@this, name, module, configure);
        }
        catch
        {
            provider.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Creates an instance of <see cref="ResourceFileProvider"/>, uses it to initialize
    /// a <seealso cref="FileModule"/>, and adds the latter to a module container.
    /// </summary>
    /// <typeparam name="TContainer">The type of the module container.</typeparam>
    /// <param name="this">The <typeparamref name="TContainer"/> on which this method is called.</param>
    /// <param name="baseRoute">The base route of the module.</param>
    /// <param name="assembly">The assembly where served files are contained as embedded resources.</param>
    /// <param name="pathPrefix">A string to prepend to provider-specific paths
    /// to form the name of a manifest resource in <paramref name="assembly"/>.</param>
    /// <param name="configure">A callback used to configure the module.</param>
    /// <returns><paramref name="this"/> with a <see cref="FileModule"/> added.</returns>
    /// <exception cref="NullReferenceException"><paramref name="this"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="assembly"/> is <see langword="null"/>.</exception>
    /// <seealso cref="FileModule"/>
    /// <seealso cref="ResourceFileProvider"/>
    /// <seealso cref="IWebModuleContainer.Modules"/>
    /// <seealso cref="IComponentCollection{T}.Add"/>
    public static TContainer WithEmbeddedResources<TContainer>(
        this TContainer @this,
        string baseRoute,
        Assembly assembly,
        string pathPrefix,
        Action<FileModule>? configure = null)
        where TContainer : class, IWebModuleContainer
    {
        return WithEmbeddedResources(@this, null, baseRoute, assembly, pathPrefix, configure);
    }

    /// <summary>
    /// Creates an instance of <see cref="ResourceFileProvider"/>, uses it to initialize
    /// a <seealso cref="FileModule"/>, and adds the latter to a module container,
    /// giving it the specified <paramref name="name"/> if not <see langword="null"/>.
    /// </summary>
    /// <typeparam name="TContainer">The type of the module container.</typeparam>
    /// <param name="this">The <typeparamref name="TContainer"/> on which this method is called.</param>
    /// <param name="name">The name.</param>
    /// <param name="baseRoute">The base route of the module.</param>
    /// <param name="assembly">The assembly where served files are contained as embedded resources.</param>
    /// <param name="pathPrefix">A string to prepend to provider-specific paths
    /// to form the name of a manifest resource in <paramref name="assembly"/>.</param>
    /// <param name="configure">A callback used to configure the module.</param>
    /// <returns><paramref name="this"/> with a <see cref="FileModule"/> added.</returns>
    /// <exception cref="NullReferenceException"><paramref name="this"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="assembly"/> is <see langword="null"/>.</exception>
    /// <seealso cref="FileModule"/>
    /// <seealso cref="ResourceFileProvider"/>
    /// <seealso cref="IWebModuleContainer.Modules"/>
    /// <seealso cref="IComponentCollection{T}.Add"/>
    public static TContainer WithEmbeddedResources<TContainer>(
        this TContainer @this,
        string? name,
        string baseRoute,
        Assembly assembly,
        string pathPrefix,
        Action<FileModule>? configure = null)
        where TContainer : class, IWebModuleContainer
    {
        FileModule module = new(baseRoute, new ResourceFileProvider(assembly, pathPrefix));
        return WithModule(@this, name, module, configure);
    }

    /// <summary>
    /// Creates an instance of <see cref="ZipFileProvider"/> using a file-system path, uses it to initialize
    /// a <seealso cref="FileModule"/>, and adds the latter to a module container.
    /// </summary>
    /// <typeparam name="TContainer">The type of the module container.</typeparam>
    /// <param name="this">The <typeparamref name="TContainer"/> on which this method is called.</param>
    /// <param name="baseRoute">The base route of the module.</param>
    /// <param name="zipFilePath">The local path of the Zip file.</param>
    /// <param name="configure">A callback used to configure the module.</param>
    /// <returns><paramref name="this"/> with a <see cref="FileModule"/> added.</returns>
    /// <exception cref="NullReferenceException"><paramref name="this"/> is <see langword="null"/>.</exception>
    /// <seealso cref="FileModule"/>
    /// <seealso cref="ZipFileProvider"/>
    /// <seealso cref="IWebModuleContainer.Modules"/>
    /// <seealso cref="IComponentCollection{T}.Add"/>
    public static TContainer WithZipFile<TContainer>(
        this TContainer @this,
        string baseRoute,
        string zipFilePath,
        Action<FileModule>? configure = null)
        where TContainer : class, IWebModuleContainer
    {
        return WithZipFile(@this, null, baseRoute, zipFilePath, configure);
    }

    /// <summary>
    /// Creates an instance of <see cref="ZipFileProvider"/> using a file-system path, uses it to initialize
    /// a <seealso cref="FileModule"/>, and adds the latter to a module container,
    /// giving it the specified <paramref name="name"/> if not <see langword="null"/>.
    /// </summary>
    /// <typeparam name="TContainer">The type of the module container.</typeparam>
    /// <param name="this">The <typeparamref name="TContainer"/> on which this method is called.</param>
    /// <param name="name">The name.</param>
    /// <param name="baseRoute">The base route of the module.</param>
    /// <param name="zipFilePath">The zip file-system path.</param>
    /// <param name="configure">A callback used to configure the module.</param>
    /// <returns><paramref name="this"/> with a <see cref="FileModule"/> added.</returns>
    /// <exception cref="NullReferenceException"><paramref name="this"/> is <see langword="null"/>.</exception>
    /// <seealso cref="FileModule"/>
    /// <seealso cref="ZipFileProvider"/>
    /// <seealso cref="IWebModuleContainer.Modules"/>
    /// <seealso cref="IComponentCollection{T}.Add"/>
    public static TContainer WithZipFile<TContainer>(
        this TContainer @this,
        string? name,
        string baseRoute,
        string zipFilePath,
        Action<FileModule>? configure = null)
        where TContainer : class, IWebModuleContainer
    {
        ZipFileProvider provider = new(zipFilePath);
        try
        {
            FileModule module = new(baseRoute, provider);
            return WithModule(@this, name, module, configure);
        }
        catch
        {
            provider.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Creates an instance of <see cref="ZipFileProvider"/> using a zip file as stream, uses it to initialize
    /// a <seealso cref="FileModule"/>, and adds the latter to a module container.
    /// </summary>
    /// <typeparam name="TContainer">The type of the module container.</typeparam>
    /// <param name="this">The <typeparamref name="TContainer"/> on which this method is called.</param>
    /// <param name="baseRoute">The base route of the module.</param>
    /// <param name="zipFileStream">The zip file as stream.</param>
    /// <param name="configure">A callback used to configure the module.</param>
    /// <returns><paramref name="this"/> with a <see cref="FileModule"/> added.</returns>
    /// <exception cref="NullReferenceException"><paramref name="this"/> is <see langword="null"/>.</exception>
    /// <seealso cref="FileModule"/>
    /// <seealso cref="ZipFileProvider"/>
    /// <seealso cref="IWebModuleContainer.Modules"/>
    /// <seealso cref="IComponentCollection{T}.Add"/>
    public static TContainer WithZipFileStream<TContainer>(
        this TContainer @this,
        string baseRoute,
        Stream zipFileStream,
        Action<FileModule>? configure = null)
        where TContainer : class, IWebModuleContainer
    {
        return WithZipFileStream(@this, null, baseRoute, zipFileStream, configure);
    }

    /// <summary>
    /// Creates an instance of <see cref="ZipFileProvider"/> using a zip file as stream, uses it to initialize
    /// a <seealso cref="FileModule"/>, and adds the latter to a module container,
    /// giving it the specified <paramref name="name"/> if not <see langword="null"/>.
    /// </summary>
    /// <typeparam name="TContainer">The type of the module container.</typeparam>
    /// <param name="this">The <typeparamref name="TContainer"/> on which this method is called.</param>
    /// <param name="name">The name.</param>
    /// <param name="baseRoute">The base route of the module.</param>
    /// <param name="zipFileStream">The zip file as stream.</param>
    /// <param name="configure">A callback used to configure the module.</param>
    /// <returns><paramref name="this"/> with a <see cref="FileModule"/> added.</returns>
    /// <exception cref="NullReferenceException"><paramref name="this"/> is <see langword="null"/>.</exception>
    /// <seealso cref="FileModule"/>
    /// <seealso cref="ZipFileProvider"/>
    /// <seealso cref="IWebModuleContainer.Modules"/>
    /// <seealso cref="IComponentCollection{T}.Add"/>
    public static TContainer WithZipFileStream<TContainer>(
        this TContainer @this,
        string? name,
        string baseRoute,
        Stream zipFileStream,
        Action<FileModule>? configure = null)
        where TContainer : class, IWebModuleContainer
    {
        ZipFileProvider provider = new(zipFileStream);
        try
        {
            FileModule module = new(baseRoute, provider);
            return WithModule(@this, name, module, configure);
        }
        catch
        {
            provider.Dispose();
            throw;
        }
    }
}