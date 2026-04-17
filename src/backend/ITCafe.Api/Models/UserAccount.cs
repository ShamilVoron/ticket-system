using System.ComponentModel.DataAnnotations;
using System.Linq;
using MongoDB.Bson.Serialization.Attributes;

namespace ITCafe.Api.Models;

public class UserAccount
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty; // MVP only, plain text
    public string Role { get; set; } = "client"; // client | coordinator | super_admin | field_engineer
    public string FullName { get; set; } = string.Empty;
}
