using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Domain.Rights;
using FluentValidation;

namespace Cemetery.Application.Rights;

public sealed record ListSiteDocuments(Guid GraveSiteId);

public sealed class ListSiteDocumentsHandler(ICallerContext caller, ILayoutRepository layouts, IRightsRepository rights) : IQueryHandler<ListSiteDocuments, IReadOnlyList<DocumentView>>
{
    public async Task<IReadOnlyList<DocumentView>> Handle(ListSiteDocuments query, CancellationToken cancellationToken)
    {
        _ = caller.OrganizationId;
        _ = await layouts.FindGraveSiteAsync(query.GraveSiteId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("grave_site.not_found");
        var documents = await rights.ListDocumentsAsync(query.GraveSiteId, cancellationToken).ConfigureAwait(false);
        return documents.Select(View).ToArray();
    }

    private static DocumentView View(SiteDocument document) =>
        new(document.Id, document.GraveSiteId, document.FileName, document.ContentType, document.ByteSize);
}

public sealed record AddSiteDocument(Guid GraveSiteId, string FileName, string ContentType, string ContentBase64, string? Kind);

public sealed class AddSiteDocumentValidator : AbstractValidator<AddSiteDocument>
{
    public AddSiteDocumentValidator() => RuleFor(command => command.GraveSiteId).NotEmpty().WithErrorCode("grave_site.not_found");
}

public sealed class AddSiteDocumentHandler(
    ICallerContext caller,
    ILayoutRepository layouts,
    IRightsRepository rights,
    IFileStorage storage,
    IVirusScan virusScan,
    IUnitOfWork unitOfWork) : ICommandHandler<AddSiteDocument, DocumentView>
{
    public async Task<DocumentView> Handle(AddSiteDocument command, CancellationToken cancellationToken)
    {
        _ = await layouts.FindGraveSiteAsync(command.GraveSiteId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("grave_site.not_found");
        var decoded = DocumentBytes.Decode(command.FileName, command.ContentType, command.ContentBase64);
        await virusScan.ScanAsync(decoded.Content, cancellationToken).ConfigureAwait(false);
        var key = await storage.PutAsync(caller.OrganizationId, decoded.Content, cancellationToken).ConfigureAwait(false);
        var document = SiteDocument.Store(caller.OrganizationId, command.GraveSiteId, null, DocumentKinds.Parse(command.Kind), decoded.FileName, decoded.ContentType, decoded.Content.Length, key);
        await rights.AddDocumentAsync(document, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new DocumentView(document.Id, document.GraveSiteId, document.FileName, document.ContentType, document.ByteSize);
    }
}

public sealed record DownloadDocument(Guid DocumentId);

public sealed class DownloadDocumentHandler(ICallerContext caller, IRightsRepository rights, IFileStorage storage) : IQueryHandler<DownloadDocument, FileDownload>
{
    public async Task<FileDownload> Handle(DownloadDocument query, CancellationToken cancellationToken)
    {
        _ = caller.OrganizationId;
        var document = await rights.FindDocumentAsync(query.DocumentId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("document.not_found");
        return await Read(storage, document, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<FileDownload> Read(IFileStorage storage, SiteDocument document, CancellationToken cancellationToken)
    {
        var content = await storage.OpenAsync(document.StorageKey, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("document.not_found");
        return new FileDownload(content, document.ContentType, document.FileName);
    }
}

public sealed record DownloadMyDocument(Guid DocumentId);

public sealed class DownloadMyDocumentHandler(ICallerContext caller, IRightsRepository rights, IFileStorage storage) : IQueryHandler<DownloadMyDocument, FileDownload>
{
    public async Task<FileDownload> Handle(DownloadMyDocument query, CancellationToken cancellationToken)
    {
        var holder = await rights.FindHolderByUserAsync(caller.UserId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("document.not_found");
        var document = await rights.FindDocumentAsync(query.DocumentId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("document.not_found");
        var owned = await rights.ListRightsForHolderAsync(holder.Id, cancellationToken).ConfigureAwait(false);
        if (!owned.Any(right => right.GraveSiteId == document.GraveSiteId && right.Status == GraveRightStatus.Active))
            throw new NotFoundException("document.not_found");
        return await DownloadDocumentHandler.Read(storage, document, cancellationToken).ConfigureAwait(false);
    }
}
