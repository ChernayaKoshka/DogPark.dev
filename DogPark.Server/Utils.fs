[<AutoOpen>]
module DogPark.Utils

open System
open System.IO
open System.Net.Mime
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.StaticFiles
open Microsoft.Extensions.FileProviders
open Microsoft.Extensions.Primitives
open Microsoft.Net.Http.Headers

// borrowed and fsharp-ified from https://github.com/dazinator/blazor-sample-multi-spa/blob/0f96041d9d6628fef36f2a1e5c8260e7ec421e0e/Server/BlazorAppBuilderExtensions.cs#L62
let getBlazorFrameworkStaticFileOptions (fileProvider: IFileProvider) requestPath =
    let contentTypeProvider = FileExtensionContentTypeProvider()

    let addMapping extension contentType =
        if contentTypeProvider.Mappings.ContainsKey(extension) |> not then
            contentTypeProvider.Mappings.Add(extension, contentType)

    addMapping ".dll" MediaTypeNames.Application.Octet
    addMapping ".dat" MediaTypeNames.Application.Octet
    // We unconditionally map pdbs as there will be no pdbs in the output folder for
    // release builds unless BlazorEnableDebugging is explicitly set to true.
    addMapping ".pdb" MediaTypeNames.Application.Octet
    addMapping ".br" MediaTypeNames.Application.Octet

    StaticFileOptions(
        FileProvider = fileProvider,
        ContentTypeProvider = contentTypeProvider,
        RequestPath = PathString requestPath,

        // Static files middleware will try to use application/x-gzip as the content
        // type when serving a file with a gz extension. We need to correct that before
        // sending the file.
        OnPrepareResponse = fun fileContext ->
            // At this point we mapped something from the /_framework
            fileContext.Context.Response.Headers.Append(HeaderNames.CacheControl, StringValues "no-cache");

            let requestPath = fileContext.Context.Request.Path;
            let fileExtension = Path.GetExtension(requestPath.Value);
            if (String.Equals(fileExtension, ".gz") || String.Equals(fileExtension, ".br")) then
                // When we are serving framework files (under _framework/ we perform content negotiation
                // on the accept encoding and replace the path with <<original>>.gz|br if we can serve gzip or brotli content
                // respectively.
                // Here we simply calculate the original content type by removing the extension and apply it
                // again.
                // When we revisit this, we should consider calculating the original content type and storing it
                // in the request along with the original target path so that we don't have to calculate it here.
                let originalPath = Path.GetFileNameWithoutExtension(requestPath.Value)
                match (contentTypeProvider.TryGetContentType(originalPath)) with
                | (true, originalContentType) ->
                    fileContext.Context.Response.ContentType <- originalContentType
                | _ ->
                    ()
    )