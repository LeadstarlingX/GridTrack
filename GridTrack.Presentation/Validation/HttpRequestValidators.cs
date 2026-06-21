using FluentValidation;
using GridTrack.Presentation.Controllers.Deliveries;
using GridTrack.Presentation.Controllers.DistrictGroups;

namespace GridTrack.Presentation.Validation;

// Validators for the HTTP request DTOs. Discovered by AddValidatorsFromAssembly and run by
// ValidationActionFilter → a failing request returns 400 with the field errors.

public sealed class CreateDeliveryHttpRequestValidator : AbstractValidator<CreateDeliveryHttpRequest>
{
    public CreateDeliveryHttpRequestValidator()
    {
        RuleFor(x => x.Lat).InclusiveBetween(-90, 90);
        RuleFor(x => x.Lng).InclusiveBetween(-180, 180);
    }
}

public sealed class PickUpDeliveryHttpRequestValidator : AbstractValidator<PickUpDeliveryHttpRequest>
{
    public PickUpDeliveryHttpRequestValidator()
    {
        RuleFor(x => x.Lat).InclusiveBetween(-90, 90);
        RuleFor(x => x.Lng).InclusiveBetween(-180, 180);
    }
}

public sealed class CancelDeliveryHttpRequestValidator : AbstractValidator<CancelDeliveryHttpRequest>
{
    public CancelDeliveryHttpRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class FlagAnomalyHttpRequestValidator : AbstractValidator<FlagAnomalyHttpRequest>
{
    public FlagAnomalyHttpRequestValidator()
    {
        RuleFor(x => x.Type).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class CreateDistrictGroupHttpRequestValidator : AbstractValidator<CreateDistrictGroupHttpRequest>
{
    public CreateDistrictGroupHttpRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DistrictIds).NotEmpty();
    }
}

public sealed class UpdateDistrictGroupHttpRequestValidator : AbstractValidator<UpdateDistrictGroupHttpRequest>
{
    public UpdateDistrictGroupHttpRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DistrictIds).NotEmpty();
    }
}
