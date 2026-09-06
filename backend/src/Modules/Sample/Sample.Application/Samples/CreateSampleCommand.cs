using DomainSample = Sample.Domain.Sample;
using FluentValidation;
using MediatR;
using Sample.Application.Abstractions;
using Sample.Application.Dtos;
using Sample.Domain;

namespace Sample.Application.Samples;

public sealed record CreateSampleCommand(Guid ShopId, string Name, SampleStatus Status) : IRequest<SampleDetail>;

public sealed class CreateSampleCommandValidator : AbstractValidator<CreateSampleCommand>
{
    public CreateSampleCommandValidator()
    {
        RuleFor(x => x.ShopId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Status).IsInEnum();
    }
}

public sealed class CreateSampleCommandHandler(ISampleRepository repository, TimeProvider timeProvider)
    : IRequestHandler<CreateSampleCommand, SampleDetail>
{
    public async Task<SampleDetail> Handle(CreateSampleCommand request, CancellationToken cancellationToken)
    {
        var sample = DomainSample.Create(request.ShopId, request.Name, request.Status, timeProvider);
        await repository.AddAsync(sample, cancellationToken);
        return SampleDetail.FromDomain(sample);
    }
}
