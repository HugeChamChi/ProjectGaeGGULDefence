using BACKND.Database;
using System.Collections.Generic;

[Table("shop_item", TableType.FlexibleTable)]
public class ShopDBItem : BaseModel
{
    [PrimaryKey(AutoIncrement = true)]
    [Column("id", DatabaseType.Int32, NotNull = true)]
    public int Id { get; set; }

    [Column("shopitem", DatabaseType.Json)]
    public List<int> Shopitem { get; set; } = new List<int>();

    [Column("shop_name", DatabaseType.String)]
    public string ShopType { get; set; }
}