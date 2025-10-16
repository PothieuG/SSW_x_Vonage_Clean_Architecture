using MediatR;
using Microsoft.AspNetCore.Mvc;
using SSW_x_Vonage_Clean_Architecture.Application.UseCases.Teams.Commands.AddHeroToTeam;
using SSW_x_Vonage_Clean_Architecture.Application.UseCases.Teams.Commands.CompleteMission;
using SSW_x_Vonage_Clean_Architecture.Application.UseCases.Teams.Commands.CreateTeam;
using SSW_x_Vonage_Clean_Architecture.Application.UseCases.Teams.Commands.ExecuteMission;
using SSW_x_Vonage_Clean_Architecture.Application.UseCases.Teams.Queries.GetAllTeams;
using SSW_x_Vonage_Clean_Architecture.Application.UseCases.Teams.Queries.GetTeam;
using SSW_x_Vonage_Clean_Architecture.WebApi.Extensions;
using TeamDto = SSW_x_Vonage_Clean_Architecture.Application.UseCases.Teams.Queries.GetAllTeams.TeamDto;

namespace SSW_x_Vonage_Clean_Architecture.WebApi.Endpoints;

public static class TeamEndpoints
{
    public static void MapTeamEndpoints(this WebApplication app)
    {
        var group = app.MapApiGroup("teams");

        group
            .MapPost("/", async (ISender sender, CreateTeamCommand command, CancellationToken ct) =>
            {
                var result = await sender.Send(command, ct);
                return result.Match(_ => TypedResults.Created(), CustomResult.Problem);
            })
            .WithName("CreateTeam")
            .ProducesPost();

        group
            .MapGet("/", async (ISender sender, CancellationToken ct) =>
            {
                var results = await sender.Send(new GetAllTeamsQuery(), ct);
                return Results.Ok(results);
            })
            .WithName("GetAllTeams")
            .ProducesGet<TeamDto[]>();

        group
            .MapPost("/{teamId:guid}/heroes/{heroId:guid}", async (
                ISender sender,
                Guid teamId,
                Guid heroId,
                CancellationToken ct) =>
            {
                var command = new AddHeroToTeamCommand(teamId, heroId);
                var result = await sender.Send(command, ct);
                return result.Match(_ => TypedResults.Created(), CustomResult.Problem);
            })
            .WithName("AddHeroToTeam")
            .ProducesPost();

        group
            .MapGet("/{teamId:guid}",
                async (ISender sender, Guid teamId, CancellationToken ct) =>
                {
                    var query = new GetTeamQuery(teamId);
                    var results = await sender.Send(query, ct);
                    return results.Match(TypedResults.Ok, CustomResult.Problem);
                })
            .WithName("GetTeam")
            .ProducesGet<TeamDto>();

        group
            .MapPost("/{teamId:guid}/execute-mission",
                async (ISender sender, Guid teamId, [FromBody] ExecuteMissionCommand command, CancellationToken ct) =>
                {
                    command.TeamId = teamId;
                    var result = await sender.Send(command, ct);
                    return result.Match(TypedResults.Ok, CustomResult.Problem);
                })
            .WithName("ExecuteMission")
            .ProducesPost();

        group
            .MapPost("/{teamId:guid}/complete-mission",
                async (ISender sender, Guid teamId, CancellationToken ct) =>
                {
                    var command = new CompleteMissionCommand(teamId);
                    var result = await sender.Send(command, ct);
                    return result.Match(TypedResults.Ok, CustomResult.Problem);
                })
            .WithName("CompleteMission")
            .ProducesPost();
    }
}