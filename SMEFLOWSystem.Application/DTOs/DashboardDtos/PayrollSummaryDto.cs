namespace SMEFLOWSystem.Application.DTOs.DashboardDtos;

public class PayrollSummaryDto
{
    public int Month { get; set; }
    public int Year { get; set; }
    public int DraftCount { get; set; }
    public int PublishedCount { get; set; }
    public int PaidCount { get; set; }
    public decimal TotalNetSalary { get; set; }       // SUM NetSalary tất cả status
    public decimal TotalPaidSalary { get; set; }      // SUM NetSalary của Paid only
}
