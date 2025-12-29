using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RRCServices.League.DTO
{

    public enum TrophyType
    {
        JR,
        DB
    }

    public sealed class LeagueRowDto
    {
        public int Rank { get; init; }
        public int RunnerId { get; init; }
        public string RunnerName { get; init; } = "";
        public string? Ukan { get; init; }

        public TrophyType League { get; init; }
        public int Points { get; init; }
        public int TimeDiffSeconds { get; init; }
    }

    public sealed class LeagueTableDto
    {
        public DateTime SeasonStart { get; init; }
        public DateTime SeasonEnd { get; init; }
        public DateTime GeneratedAtUtc { get; init; }

        public IReadOnlyList<LeagueRowDto> Rows { get; init; } = Array.Empty<LeagueRowDto>();
    }

}
