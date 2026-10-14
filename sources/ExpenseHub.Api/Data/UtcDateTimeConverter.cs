using System;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ExpenseHub.Api.Data;

/// <summary>
/// Garante que todo <see cref="DateTime"/> seja gravado em UTC e lido com <see cref="DateTimeKind.Utc"/>,
/// para que a API devolva horários com o sufixo <c>Z</c>.
/// </summary>
internal sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(
            value => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc),
            value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
    {
    }
}
