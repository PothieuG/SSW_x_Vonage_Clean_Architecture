using SSW_x_Vonage_Clean_Architecture.Application.Common.Interfaces;
using SSW_x_Vonage_Clean_Architecture.Domain.Teams;

namespace SSW_x_Vonage_Clean_Architecture.Application.UseCases.Teams.Commands.CreateTeam;

public sealed record CreateTeamCommand(string Name) : IRequest<ErrorOr<Success>>;

internal sealed class CreateTeamCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<CreateTeamCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(CreateTeamCommand request, CancellationToken cancellationToken)
    {
        var team = Team.Create(request.Name);

        await dbContext.Teams.AddAsync(team, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new Success();
    }
}

internal sealed class CreateTeamCommandValidator : AbstractValidator<CreateTeamCommand>
{
    public CreateTeamCommandValidator()
    {
        RuleFor(v => v.Name)
            .NotEmpty();
    }
}