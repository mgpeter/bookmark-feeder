using BookmarkFeeder.WebApi.Dtos;
using FluentValidation;

namespace BookmarkFeeder.WebApi.Validators;

public class CreateBookmarkRequestValidator : AbstractValidator<CreateBookmarkRequest>
{
    public CreateBookmarkRequestValidator()
    {
        RuleFor(x => x.Url).NotEmpty().MaximumLength(2048).Must(BeAValidUrl).WithMessage("'Url' must be a valid absolute URL.");
        RuleFor(x => x.Title).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.FaviconUrl).MaximumLength(2048);
        RuleFor(x => x.SourceFolder).MaximumLength(1024);
    }

    private static bool BeAValidUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out _);
}

public class UpdateBookmarkRequestValidator : AbstractValidator<UpdateBookmarkRequest>
{
    public UpdateBookmarkRequestValidator()
    {
        RuleFor(x => x.Title).MaximumLength(500).When(x => x.Title is not null);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.FaviconUrl).MaximumLength(2048);
    }
}

public class BatchCreateRequestValidator : AbstractValidator<BatchCreateRequest>
{
    public BatchCreateRequestValidator()
    {
        RuleFor(x => x.Bookmarks).NotNull();
        RuleForEach(x => x.Bookmarks).ChildRules(item =>
        {
            item.RuleFor(b => b.Url).NotEmpty().MaximumLength(2048);
            item.RuleFor(b => b.Title).MaximumLength(500);
            item.RuleFor(b => b.SourceFolder).MaximumLength(1024);
        });
    }
}

public class CreateTagRequestValidator : AbstractValidator<CreateTagRequest>
{
    public CreateTagRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Color).MaximumLength(32);
    }
}

public class UpdateTagRequestValidator : AbstractValidator<UpdateTagRequest>
{
    public UpdateTagRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Color).MaximumLength(32);
    }
}

public class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}
