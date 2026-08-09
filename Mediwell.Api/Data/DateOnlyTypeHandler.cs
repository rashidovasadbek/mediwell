using System.Data;
using Dapper;

namespace Mediwell.Api.Data;

public  class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.Value = value.ToDateTime(TimeOnly.MinValue);
        parameter.DbType = DbType.Date;
    }

    public override DateOnly Parse(object value)
    {
        return value switch
        {
            DateOnly d => d,
            DateTime dt => DateOnly.FromDateTime(dt),
            _ => DateOnly.Parse(value.ToString()!)
        };
    }
}