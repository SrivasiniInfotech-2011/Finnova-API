using Finnova.Models.Contracts.DemandProcessing;
using Finnova.Models.Domain.Entities;

namespace Finnova.Service.DemandProcessing.Mappers;

public static class DemandMappers
{
    public static DemandRowResponse ToResponse(this DemandRow row) => new(row.Id, row.Revision.DemandRunId, row.Revision.RevisionNumber, row.AccountId, row.CustomerId, row.LineOfBusinessId, row.BranchId, row.AccountDisplay, row.CustomerDisplay, row.Revision.DemandRun.PeriodYear, row.Revision.DemandRun.PeriodMonth, row.Revision.DemandRun.Frequency, row.CurrentDue, row.Arrears, row.Odi, row.OtherCharges, row.TotalCharges, row.TotalDemand, row.IsZeroDemand, row.Status, row.ScheduleReference, row.ProcessingCutoffReference, row.OrderingKey);
    public static DemandBatchResponse ToResponse(this DemandBatch batch) => new(batch.Id, batch.DemandRunId, batch.RevisionNumber, batch.BranchId, batch.LineOfBusinessId, batch.Frequency, batch.PeriodYear, batch.PeriodMonth, batch.Status, batch.RowCount, batch.CurrentDueTotal, batch.ArrearsTotal, batch.OdiTotal, batch.OtherChargesTotal, batch.TotalCharges, batch.TotalDemand, batch.FailureClass?.ToString(), batch.FailureCode, batch.FailureReason, batch.LastSpoolActorId, batch.LastSpooledAtUtc);
    public static DemandFailureResponse ToResponse(this DemandFailure failure) => new(failure.FailureClass, failure.Code, failure.Message, failure.AccountId, failure.DemandBatchId, failure.CreatedAtUtc);
}
