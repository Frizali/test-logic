using System.ComponentModel.DataAnnotations;

public class Program
{
    public static void Main(string[] args)
    {
        //var x = new List<DayExecute>()
        //{
        //    new DayExecute(){ DayId = DayOfWeek.Monday },
        //    new DayExecute(){ DayId = DayOfWeek.Tuesday },
        //    new DayExecute(){ DayId = DayOfWeek.Wednesday },
        //    new DayExecute(){ DayId = DayOfWeek.Thursday },
        //    new DayExecute(){ DayId = DayOfWeek.Friday },
        //    new DayExecute(){ DayId = DayOfWeek.Saturday },
        //}.ToDictionary(x => x.DayId);

        //var startDate = new DateTime(2026, 01, 05);
        //var endDate = new DateTime(2026, 01, 17);
        //var totalWeek = 0;

        //for(var date = startDate; date <= endDate; date = date.AddDays(1))
        //{
        //    var prevDayExec = false;

        //    if(date.DayOfWeek > DayOfWeek.Monday)
        //    {
        //        x.TryGetValue((DayOfWeek)((int)date.DayOfWeek) - 1, out DayExecute? prevDay);
        //        prevDayExec = prevDay?.Execute ?? false;
        //    }

        //    if(date.DayOfWeek == DayOfWeek.Monday || prevDayExec)
        //    {
        //        if(!x.TryGetValue(date.DayOfWeek, out DayExecute? day))
        //             throw new InvalidOperationException($"Bruh");

        //        day.Execute = true;
        //    }

        //    if(date.DayOfWeek == DayOfWeek.Saturday && !x.Any(x => !x.Value.Execute))
        //    {
        //        totalWeek++;
        //        x.ToList().ForEach(x => x.Value.Execute = true);
        //    }
        //}

        //if (totalWeek > 0)
        //    Console.WriteLine($"Sukses Bro, Dipotong {totalWeek * 0.5}");

        var leaveHistories = new List<LeaveHistory>() { 
            new LeaveHistory(new DateTime(2025, 04, 30), LeaveType.Leave, 1),
            new LeaveHistory(new DateTime(2015, 04, 30), LeaveType.Leave, 1)
        };

        DateTime empJoin = new DateTime(2015, 01, 15);
        DateTime lastBalanceDate = empJoin;
        DateTime date = new DateTime(2026, 01, 30);

        var longLeaveYears = GetLongLeaveYears(empJoin, date, out int firstLongLeaveYear);
        var firstLongLeaveDate = new DateTime(firstLongLeaveYear, empJoin.Month, empJoin.Day);
        
        // Melakukan penambahan bulanan sebelum masuk ke periode long leave
        if (lastBalanceDate < firstLongLeaveDate)
        {
            PostAdditionalMonthly(empJoin, lastBalanceDate, firstLongLeaveDate, leaveHistories);
            CalculateYearlyExpired(empJoin, firstLongLeaveDate, leaveHistories);
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

                    // Balance akan expired jika tanggal posting melebihi tanggal expired
                    if(date > longLeaveExpired)
                        CalculateLongLeaveExpired(longLeaveDate, longLeaveExpired, leaveHistories);
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
                PostAdditionalMonthly(empJoin, cutOffDateAdditional, postDateLimit, leaveHistories);
            }

            yearIndex++;
        }

        Recalculate(leaveHistories);
    }

    public static void Recalculate(List<LeaveHistory> leaveHistories)
    {
        leaveHistories = leaveHistories.OrderBy(l => l.DateID).ThenBy(l => l.Type).ToList();

        double tmpTotalBalance = 0;
        double tmpCurrentBalance = 0;
        double tmpLeaveTaken = 0;
        double tmpExpiredLeave = 0;

        foreach(var leave in leaveHistories)
        {
            switch (leave.Type)
            {
                case LeaveType.Additional:
                    leave.LastBalance = tmpCurrentBalance;
                    tmpTotalBalance += leave.TotalDay;
                    tmpCurrentBalance += leave.TotalDay;
                    leave.CurrentBalance = tmpCurrentBalance;
                    break;
                case LeaveType.Balance:
                case LeaveType.Long:
                    tmpTotalBalance += leave.TotalDay;
                    tmpCurrentBalance = leave.TotalDay;
                    leave.CurrentBalance = tmpCurrentBalance;
                    break;
                case LeaveType.Leave:
                    tmpLeaveTaken += leave.TotalDay;
                    leave.LastBalance = tmpCurrentBalance;
                    tmpCurrentBalance -= leave.TotalDay;
                    leave.CurrentBalance = tmpCurrentBalance;
                    break;
                case LeaveType.Expired:
                    tmpExpiredLeave += leave.TotalDay;
                    leave.LastBalance = tmpCurrentBalance;
                    tmpCurrentBalance -= leave.TotalDay;
                    leave.CurrentBalance = tmpCurrentBalance;
                    break;
            }
        }

        foreach(var leave in leaveHistories)
            Console.WriteLine($"Type: {leave.Type.ToString()}, Date: {leave.DateID.Date.ToString("MMMM dd yyyy")}, LastBalance: {leave.LastBalance}, TotalDay: {leave.TotalDay}, CurrentBalance: {leave.CurrentBalance}");
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

    public static void PostAdditionalMonthly(DateTime empJoin, DateTime startDate, DateTime endDate, List<LeaveHistory> leaveHistories)
    {
        var tmpAdditionalDate = new DateTime(startDate.Year, startDate.Month, 1);

        while (IsGetAdditionalWithLongLeave(empJoin) ? tmpAdditionalDate.AddMonths(1) <= endDate : tmpAdditionalDate.AddMonths(1) < endDate)
        {
            tmpAdditionalDate = tmpAdditionalDate.AddMonths(1);
            leaveHistories.Add(new LeaveHistory(tmpAdditionalDate, LeaveType.Additional, 1));
        }
    }

    public static void CalculateYearlyExpired(DateTime empJoin, DateTime date, List<LeaveHistory> leaveHistories)
    {
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
                .Where(l => l.Type == LeaveType.Leave && (l.DateID >= minRange && l.DateID < expiredDate))
                .Sum(l => l.TotalDay);

            var totalExpired = totalLeaveTaken >= (totalAdditionalBalance < 12 ? totalAdditionalBalance : 12) ? 0 : (totalAdditionalBalance < 12 ? totalAdditionalBalance : 12) - totalLeaveTaken;

            if(date > expiredDate)
                leaveHistories.Add(new LeaveHistory(expiredDate, LeaveType.Expired, totalExpired));

            minExpiredYear++;
            expiredDate = expiredDate.AddYears(1);
        }
    }

    public static void CalculateLongLeaveExpired(DateTime longLeaveDate, DateTime expiredDate, List<LeaveHistory> leaveHistories)
    {
        if (leaveHistories == null || leaveHistories.Count == 0)
            return;

        var totalBalance = leaveHistories
            .Where(l => l.Type == LeaveType.Balance || l.Type == LeaveType.Long && (l.DateID >= longLeaveDate && l.DateID < expiredDate))
            .Sum(l => l.TotalDay);

        var totalLeaveTaken = leaveHistories
            .Where(l => l.Type == LeaveType.Leave && (l.DateID >= longLeaveDate && l.DateID < expiredDate))
            .Sum(l => l.TotalDay);

        var expiredBalance = Math.Max(0, totalBalance - totalLeaveTaken);

        if (expiredBalance == 0)
            return;

        leaveHistories.Add(new LeaveHistory(expiredDate, LeaveType.Expired, expiredBalance)); 
    }

    public enum LeaveType
    {
        Additional,
        Balance,
        Long,
        Leave,
        Expired
    }

    public class LeaveHistory
    {
        public DateTime DateID { get; set; }
        public LeaveType Type { get; set; }
        public double LastBalance { get; set; }
        public double TotalDay { get; set; }
        public double CurrentBalance { get; set; }

        public LeaveHistory(DateTime dateId, LeaveType type, double totalDay)
        {
            DateID = dateId;
            Type = type;
            TotalDay = totalDay;
        }
    }

    public class DayExecute
    {
        public DayOfWeek DayId { get; set; }
        public bool Execute { get; set; } = false;
    }
}