using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IdentityProvider;

public sealed class IdentityDb : DbContext
{
    public IdentityDb(DbContextOptions<IdentityDb> options) : base(options) { }

    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<OauthClient> Clients => Set<OauthClient>();
    public DbSet<AuthCode> AuthCodes => Set<AuthCode>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserAccount>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Username).HasColumnName("username");
            e.Property(x => x.PasswordHash).HasColumnName("password_hash");
            e.Property(x => x.Email).HasColumnName("email");
            e.Property(x => x.FirstName).HasColumnName("first_name");
            e.Property(x => x.LastName).HasColumnName("last_name");
            e.Property(x => x.Role).HasColumnName("role");
        });

        modelBuilder.Entity<OauthClient>(e =>
        {
            e.ToTable("oauth_clients");
            e.HasKey(x => x.ClientId);
            e.Property(x => x.ClientId).HasColumnName("client_id");
            e.Property(x => x.ClientSecret).HasColumnName("client_secret");
            e.Property(x => x.RedirectUris).HasColumnName("redirect_uris");
        });

        modelBuilder.Entity<AuthCode>(e =>
        {
            e.ToTable("auth_codes");
            e.HasKey(x => x.Code);
            e.Property(x => x.Code).HasColumnName("code");
            e.Property(x => x.ClientId).HasColumnName("client_id");
            e.Property(x => x.Username).HasColumnName("username");
            e.Property(x => x.RedirectUri).HasColumnName("redirect_uri");
            e.Property(x => x.Scope).HasColumnName("scope");
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        });
    }
}

public sealed class UserAccount
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string Role { get; set; } = "User";
}

public sealed class OauthClient
{
    [Key]
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string RedirectUris { get; set; } = "";
}

public sealed class AuthCode
{
    [Key]
    public string Code { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string Username { get; set; } = "";
    public string RedirectUri { get; set; } = "";
    public string Scope { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
}
