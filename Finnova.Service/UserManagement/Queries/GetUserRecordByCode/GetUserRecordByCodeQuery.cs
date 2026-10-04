using Finnova.Models.Contracts.UserManagement;
using MediatR;

namespace Finnova.Service.UserManagement.Queries.GetUserRecordByCode;

/// <summary>Serves Modify (editable) and Query (read-only) — the read payload is identical (R11.1/R12.1).</summary>
public record GetUserRecordByCodeQuery(string Code) : IRequest<UserAccountResponse>;
