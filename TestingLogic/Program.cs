using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;

public class Program
{
    public static void Main(string[] args)
    {
        var longLeaveRemarkTaken = new List<string>()
        {
            "LAL",
            "LAL05"
        };

        var leaveHistories = new List<LeaveHistory>() { 
            new LeaveHistory(new DateTime(2025, 04, 30), LeaveType.Leave, 1),
            new LeaveHistory(new DateTime(2015, 04, 30), LeaveType.Leave, 1),
            new LeaveHistory(new DateTime(2021, 02, 10), LeaveType.Leave, 1, "LAL"),
        };

        DateTime empJoin = new DateTime(2015, 01, 04);
        DateTime lastBalanceDate = empJoin;
        DateTime date = new DateTime(2026, 01, 30);

        var longLeaveYears = GetLongLeaveYears(empJoin, date, out int firstLongLeaveYear);
        var firstLongLeaveDate = new DateTime(firstLongLeaveYear, empJoin.Month, empJoin.Day);
        
        // Melakukan penambahan bulanan sebelum masuk ke periode long leave
        if (lastBalanceDate < firstLongLeaveDate)
        {
            PostAdditionalMonthly(empJoin, lastBalanceDate, firstLongLeaveDate, leaveHistories, false);
            CalculateYearlyExpired(empJoin, firstLongLeaveDate, leaveHistories, longLeaveRemarkTaken);
        }

        var maxYearIndex = longLeaveYears.Count - 1;
        var yearIndex = 0;
        foreach(var year in longLeaveYears)
        {
            // Penambahan cuti perbulan di stop sampai 2 tahun kedepan
            var cutOffDateAdditional = new DateTime(year, empJoin.Month, empJoin.Day).AddYears(2);
            var nextCutOffDateAdditional = date;

            // Long leave terjadi 2 tahun berurut
            for(int i = 0; i <= 1; i++)
            {
                var longLeaveDate = new DateTime(year, empJoin.Month, empJoin.Day).AddYears(i);
                var longLeaveExpired = longLeaveDate.AddMonths(6);

                // Pengecekan last balance date
                if(longLeaveDate > lastBalanceDate && date > longLeaveDate)
                {
                    // Tambahkan long leave balance 30
                    leaveHistories.Add(new LeaveHistory(longLeaveDate, LeaveType.Long, 30));
                    if (i == 0)
                        CalcExpiredLeaveInFisrtOfLongLeave(empJoin, longLeaveDate, date, leaveHistories, longLeaveRemarkTaken);

                    // Balance akan expired jika tanggal posting melebihi tanggal expired
                    if (date > longLeaveExpired)
                        CalculateLongLeaveExpired(longLeaveDate, longLeaveExpired, leaveHistories, longLeaveRemarkTaken);
                }
            }                

            // Penambahan leave bulanan akan normal kembali jika tanggal posting lebih dari tanggal stop penambahan
            if(cutOffDateAdditional > lastBalanceDate && date > cutOffDateAdditional)
            {
                var postDateLimit = date;

                // Mencari batasan tanggal untuk penambahan bulanan
                if (yearIndex < maxYearIndex)
                {
                    // Mengecek apakah tanggal posting lebih dari tanggal batasan untuk penambahan
                    var nextPostDateLimit = new DateTime(longLeaveYears[yearIndex + 1], empJoin.Month, empJoin.Day);
                    if (date > nextPostDateLimit)
                        postDateLimit = nextPostDateLimit;
                }

                // Penambahan cuti bulanan mulai dari tanggal Cut Off Additional sampai tanggal Post Date Limit
                PostAdditionalMonthly(empJoin, cutOffDateAdditional, postDateLimit, leaveHistories, true);
            }

            yearIndex++;
        }

        Recalculate(leaveHistories, longLeaveRemarkTaken);
    }

    public static void Recalculate(List<LeaveHistory> leaveHistories, List<string> longLeaveRemarkTaken)
    {
        leaveHistories = leaveHistories.OrderBy(l => l.DateID).ThenBy(l => l.Type).ToList();

        double tmpTotalBalance = 0;
        double tmpCurrentBalance = 0;
        double tmpLeaveTaken = 0;
        double tmpLongLeave = 0;
        double tmpLongLeaveTaken = 0;
        double tmpExpiredLeave = 0;

        foreach(var leave in leaveHistories)
        {
            switch (leave.Type)
            {
                case LeaveType.Additional:
                case LeaveType.Long:
                    if (leave.Type == LeaveType.Long)
                        tmpLongLeave = leave.TotalDay;

                    leave.LastBalance = tmpCurrentBalance;
                    tmpTotalBalance += leave.TotalDay;
                    tmpCurrentBalance += leave.TotalDay;
                    leave.CurrentBalance = tmpCurrentBalance;
                    break;
                case LeaveType.Balance:
                    tmpTotalBalance += leave.TotalDay;
                    tmpCurrentBalance = leave.TotalDay;
                    leave.CurrentBalance = tmpCurrentBalance;
                    break;
                case LeaveType.Leave:
                    if (longLeaveRemarkTaken.Contains(leave.RemarkID))
                        tmpLongLeaveTaken += leave.TotalDay;

                    tmpLeaveTaken += leave.TotalDay;
                    leave.LastBalance = tmpCurrentBalance;
                    tmpCurrentBalance -= leave.TotalDay;
                    leave.CurrentBalance = tmpCurrentBalance;
                    break;
                case LeaveType.Expired:
                case LeaveType.ExpiredLongLeave:
                    if (leave.Type == LeaveType.ExpiredLongLeave)
                        tmpLongLeaveTaken += leave.TotalDay;

                    tmpExpiredLeave += leave.TotalDay;
                    leave.LastBalance = tmpCurrentBalance;
                    tmpCurrentBalance -= leave.TotalDay;
                    leave.CurrentBalance = tmpCurrentBalance;
                    break;
            }
        }

        foreach(var leave in leaveHistories)
            Console.WriteLine($"Type: {leave.Type}, Date: {leave.DateID.Date.ToString("MMMM dd yyyy")}, LastBalance: {leave.LastBalance}, TotalDay: {leave.TotalDay}, CurrentBalance: {leave.CurrentBalance}");
    }

    public static List<int> GetLongLeaveYears(DateTime empJoin, DateTime date, out int firstLongLeaveYear)
    {
        var years = new List<int>();
        var tmpLongLeaveDate = empJoin;
        var tmpFirstLLDate = empJoin;

        while(tmpLongLeaveDate.AddYears(5) < date)
        {
            tmpLongLeaveDate = tmpLongLeaveDate.AddYears(5);

            if (tmpLongLeaveDate.Year >= 2017)
                tmpLongLeaveDate = tmpLongLeaveDate.AddYears(1);
                
           years.Add(tmpLongLeaveDate.Year);
        }

        tmpFirstLLDate = tmpFirstLLDate.AddYears(5);
        if (tmpFirstLLDate.Year >= 2017)
            tmpFirstLLDate = tmpFirstLLDate.AddYears(1);

        firstLongLeaveYear = tmpFirstLLDate.Year;

        return years;
    }

    public static bool IsGetAdditionalWithLongLeave(DateTime empJoin)
    {
        var day = empJoin.Day;
        if (day >= 1 && day <= 14)
            return false;
        else
            return true;
    }

    public static void PostAdditionalMonthly(DateTime empJoin, DateTime startDate, DateTime endDate, List<LeaveHistory> leaveHistories, bool isAdditionalAfterLongLeave)
    {
        //var tmpAdditionalDate = new DateTime(startDate.Year, startDate.Month, 1);
        var tmpAdditionalDate = startDate;

        if (isAdditionalAfterLongLeave && !IsGetAdditionalWithLongLeave(empJoin))
            tmpAdditionalDate = tmpAdditionalDate.AddMonths(-1);

        while (IsGetAdditionalWithLongLeave(empJoin) ? tmpAdditionalDate.AddMonths(1) <= endDate : tmpAdditionalDate.AddMonths(1) < endDate)
        {
            tmpAdditionalDate = tmpAdditionalDate.AddMonths(1);
            leaveHistories.Add(new LeaveHistory(tmpAdditionalDate, LeaveType.Additional, 1));
        }
    }

    public static void CalculateYearlyExpired(DateTime empJoin, DateTime date, List<LeaveHistory> leaveHistories, List<string> longLeaveRemarkTaken)
    {
        date = date.AddYears(1);
        var minExpiredYear = empJoin.Year;
        var maxExpiredYear = date.Year;
        var expiredDate = new DateTime(minExpiredYear + 1, 06, 30);

        while (minExpiredYear < maxExpiredYear)
        {
            var minRange = new DateTime(minExpiredYear, 01, 01);
            var maxRange = new DateTime(minExpiredYear, 12, DateTime.DaysInMonth(minExpiredYear, 12));

            var totalAdditionalBalance = leaveHistories
                .Where(l => l.Type == LeaveType.Additional && (l.DateID >= minRange && l.DateID < maxRange))
                .Sum(l => l.TotalDay);

            var totalLeaveTaken = leaveHistories
                .Where(l => l.Type == LeaveType.Leave && (l.DateID >= minRange && l.DateID < expiredDate) && !longLeaveRemarkTaken.Contains(l.RemarkID))
                .Sum(l => l.TotalDay);

            var totalExpired = totalLeaveTaken >= totalAdditionalBalance ? 0 : totalAdditionalBalance - totalLeaveTaken;

            if(date > expiredDate)
                leaveHistories.Add(new LeaveHistory(expiredDate, LeaveType.Expired, totalExpired));

            minExpiredYear++;
            expiredDate = expiredDate.AddYears(1);
        }
    }

    public static void CalcExpiredLeaveInFisrtOfLongLeave(DateTime empJoin, DateTime longLeaveDate, DateTime date, List<LeaveHistory> leaveHistories, List<string> longLeaveRemarkTaken)
    {
        var monthOfJoin = empJoin.Month;
        var additionalYear = longLeaveDate.Year;
        var expiredDate = new DateTime(additionalYear + 1, 06, 30);

        var minRange = new DateTime(additionalYear, 01, 01);
        var maxRange = new DateTime(additionalYear, 12, DateTime.DaysInMonth(additionalYear, 12));
        var isGetAddWithLongLeave = IsGetAdditionalWithLongLeave(empJoin);

        if (monthOfJoin == 1 && !isGetAddWithLongLeave)
            return;

        if (isGetAddWithLongLeave)
        {
            var totalAdditionalBalance = leaveHistories
                .Where(l => l.Type == LeaveType.Additional && (l.DateID >= minRange && l.DateID < maxRange))
                .Sum(l => l.TotalDay);

            var totalLeaveTaken = leaveHistories
                .Where(l => l.Type == LeaveType.Leave && (l.DateID >= minRange && l.DateID < expiredDate) && !longLeaveRemarkTaken.Contains(l.RemarkID))
                .Sum(l => l.TotalDay);

            var totalExpired = totalLeaveTaken >= totalAdditionalBalance ? 0 : totalAdditionalBalance - totalLeaveTaken;

            if (date > expiredDate)
                leaveHistories.Add(new LeaveHistory(expiredDate, LeaveType.Expired, totalExpired));
        }
    }

    public static void CalculateLongLeaveExpired(DateTime longLeaveDate, DateTime expiredDate, List<LeaveHistory> leaveHistories, List<string> longLeaveRemarkTaken)
    {
        if (leaveHistories == null || leaveHistories.Count == 0)
            return;

        var totalBalance = leaveHistories
            .Where(l => l.Type == LeaveType.Balance || l.Type == LeaveType.Long && (l.DateID >= longLeaveDate && l.DateID < expiredDate))
            .Sum(l => l.TotalDay);

        var totalLeaveTaken = leaveHistories
            .Where(l => l.Type == LeaveType.Leave && (l.DateID >= longLeaveDate && l.DateID < expiredDate) && longLeaveRemarkTaken.Contains(l.RemarkID))
            .Sum(l => l.TotalDay);

        //var totalExpiredLeave = leaveHistories
        //    .Where(l => l.Type == LeaveType.Expired && (l.DateID >= longLeaveDate && l.DateID < expiredDate))
        //    .Sum(l => l.TotalDay);

        var expiredBalance = Math.Max(0, totalBalance - totalLeaveTaken);

        if (expiredBalance == 0)
            return;

        leaveHistories.Add(new LeaveHistory(expiredDate, LeaveType.ExpiredLongLeave, expiredBalance)); 
    }

    public enum LeaveType
    {
        Additional,
        Balance,
        Long,
        Leave,
        Expired,
        ExpiredLongLeave
    }

    public class LeaveHistory
    {
        public DateTime DateID { get; set; }
        public LeaveType Type { get; set; }
        public string RemarkID { get; set; }
        public double LastBalance { get; set; }
        public double TotalDay { get; set; }
        public double CurrentBalance { get; set; }

        public LeaveHistory(DateTime dateId, LeaveType type, double totalDay, string remarkId = "")
        {
            DateID = dateId;
            Type = type;
            TotalDay = totalDay;
            RemarkID = remarkId;
        }
    }
}