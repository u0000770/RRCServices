using RRCServices.League.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RRCServices.League
{

    public interface ILeagueTableService
    {
        Task<LeagueTableDto> GetLeagueTableAsync(CancellationToken ct = default);

        // Optional later: allow forcing refresh from UI/admin
        Task<LeagueTableDto> RecalculateLeagueTableAsync(CancellationToken ct = default);
    }

}
