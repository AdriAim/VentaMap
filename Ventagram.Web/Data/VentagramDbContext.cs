using Microsoft.EntityFrameworkCore;
using Ventagram.Models;

namespace Ventagram.Data;

public class VentagramDbContext(DbContextOptions<VentagramDbContext> options) : DbContext(options)
{
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<Publication> Publications => Set<Publication>();
    public DbSet<PublicationMedia> PublicationMedia => Set<PublicationMedia>();
    public DbSet<ArgentineLocality> ArgentineLocalities => Set<ArgentineLocality>();
    public DbSet<PublicationGroupType> PublicationGroupTypes => Set<PublicationGroupType>();
    public DbSet<PublicationCategory> PublicationCategories => Set<PublicationCategory>();
    public DbSet<PublicationCategoryField> PublicationCategoryFields => Set<PublicationCategoryField>();
    public DbSet<PublicationFieldValue> PublicationFieldValues => Set<PublicationFieldValue>();
    public DbSet<PublicationReportReason> PublicationReportReasons => Set<PublicationReportReason>();
    public DbSet<PublicationReport> PublicationReports => Set<PublicationReport>();
    public DbSet<FavoriteList> FavoriteLists => Set<FavoriteList>();
    public DbSet<FavoriteListItem> FavoriteListItems => Set<FavoriteListItem>();
    public DbSet<SharedPublicationList> SharedPublicationLists => Set<SharedPublicationList>();
    public DbSet<SharedPublicationListItem> SharedPublicationListItems => Set<SharedPublicationListItem>();
    public DbSet<PublicationFavorite> PublicationFavorites => Set<PublicationFavorite>();
    public DbSet<PublicationView> PublicationViews => Set<PublicationView>();
    public DbSet<SiteSuggestion> SiteSuggestions => Set<SiteSuggestion>();
    public DbSet<VentagramParameter> VentagramParameters => Set<VentagramParameter>();
    public DbSet<VerifiedOperation> VerifiedOperations => Set<VerifiedOperation>();
    public DbSet<OperationReview> OperationReviews => Set<OperationReview>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationUser>()
            .HasIndex(x => x.Email)
            .IsUnique();

        modelBuilder.Entity<ApplicationUser>()
            .HasIndex(x => x.CompanyName)
            .IsUnique();

        modelBuilder.Entity<ApplicationUser>()
            .HasIndex(x => x.CompanySlug)
            .IsUnique();

        modelBuilder.Entity<ApplicationUser>()
            .HasOne(x => x.ArgentineLocality)
            .WithMany()
            .HasForeignKey(x => x.ArgentineLocalityId);

        modelBuilder.Entity<ApplicationUser>()
            .Property(x => x.IsAdmin)
            .HasDefaultValue(false);

        modelBuilder.Entity<ApplicationUser>()
            .Property(x => x.IsDebugUser)
            .HasDefaultValue(false);

        modelBuilder.Entity<ApplicationUser>()
            .Property(x => x.CanPublish)
            .HasDefaultValue(true);

        modelBuilder.Entity<ApplicationUser>()
            .Property(x => x.CanReport)
            .HasDefaultValue(true);

        modelBuilder.Entity<ApplicationUser>()
            .Property(x => x.IsCompany)
            .HasDefaultValue(false);

        modelBuilder.Entity<ApplicationUser>()
            .Property(x => x.CompanyHeroBackgroundUrl)
            .HasMaxLength(260);

        modelBuilder.Entity<ArgentineLocality>()
            .HasIndex(x => new { x.Province, x.Locality })
            .IsUnique();

        modelBuilder.Entity<PublicationGroupType>()
            .HasKey(x => x.Id);

        modelBuilder.Entity<PublicationGroupType>()
            .Property(x => x.Id)
            .ValueGeneratedNever();

        modelBuilder.Entity<PublicationGroupType>()
            .HasIndex(x => x.Name)
            .IsUnique();

        modelBuilder.Entity<Publication>()
            .Property(x => x.Group)
            .HasConversion<byte>()
            .HasColumnType("tinyint unsigned");

        modelBuilder.Entity<Publication>()
            .HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId);

        modelBuilder.Entity<Publication>()
            .HasOne(x => x.User)
            .WithMany(x => x.Publications)
            .HasForeignKey(x => x.UserId);

        modelBuilder.Entity<Publication>()
            .Property(x => x.Price)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Publication>()
            .Property(x => x.OperationType)
            .HasConversion<byte?>()
            .HasColumnType("tinyint unsigned");

        modelBuilder.Entity<Publication>()
            .Property(x => x.UniqueViewCount)
            .HasDefaultValue(0);

        modelBuilder.Entity<Publication>()
            .Property(x => x.UniqueFavoriteCount)
            .HasDefaultValue(0);

        modelBuilder.Entity<PublicationMedia>()
            .Property(x => x.MediaType)
            .HasConversion<byte>()
            .HasColumnType("tinyint unsigned");

        modelBuilder.Entity<PublicationMedia>()
            .HasOne(x => x.Publication)
            .WithMany(x => x.MediaItems)
            .HasForeignKey(x => x.PublicationId);

        modelBuilder.Entity<PublicationMedia>()
            .HasIndex(x => new { x.PublicationId, x.SortOrder })
            .IsUnique();

        modelBuilder.Entity<PublicationMedia>()
            .HasIndex(x => new { x.PublicationId, x.MediaType, x.IsPrimary });

        modelBuilder.Entity<PublicationCategory>()
            .Property(x => x.Group)
            .HasConversion<byte>()
            .HasColumnType("tinyint unsigned");

        modelBuilder.Entity<PublicationCategory>()
            .HasIndex(x => new { x.Group, x.Name })
            .IsUnique();

        modelBuilder.Entity<PublicationCategoryField>()
            .Property(x => x.DataType)
            .HasConversion<byte>()
            .HasColumnType("tinyint unsigned");

        modelBuilder.Entity<PublicationCategoryField>()
            .Property(x => x.GroupId)
            .HasConversion<byte?>()
            .HasColumnType("tinyint unsigned")
            .HasColumnName("GroupId");

        modelBuilder.Entity<PublicationCategoryField>()
            .HasOne(x => x.Category)
            .WithMany(x => x.Fields)
            .HasForeignKey(x => x.CategoryId);

        modelBuilder.Entity<PublicationCategoryField>()
            .HasIndex(x => new { x.GroupId, x.CategoryId, x.InternalName })
            .IsUnique();

        modelBuilder.Entity<PublicationCategoryField>()
            .HasIndex(x => new { x.GroupId, x.CategoryId, x.IsActive, x.SortOrder });

        modelBuilder.Entity<PublicationFieldValue>()
            .Property(x => x.ValueNumber)
            .HasPrecision(18, 2);

        modelBuilder.Entity<PublicationFieldValue>()
            .HasOne(x => x.Publication)
            .WithMany(x => x.FieldValues)
            .HasForeignKey(x => x.PublicationId);

        modelBuilder.Entity<PublicationFieldValue>()
            .HasOne(x => x.CategoryField)
            .WithMany(x => x.Values)
            .HasForeignKey(x => x.CategoryFieldId);

        modelBuilder.Entity<PublicationFieldValue>()
            .HasIndex(x => new { x.PublicationId, x.CategoryFieldId })
            .IsUnique();

        modelBuilder.Entity<PublicationFieldValue>()
            .HasIndex(x => new { x.CategoryFieldId, x.ValueText });

        modelBuilder.Entity<PublicationFieldValue>()
            .HasIndex(x => new { x.CategoryFieldId, x.ValueNumber });

        modelBuilder.Entity<PublicationFieldValue>()
            .HasIndex(x => new { x.CategoryFieldId, x.ValueBoolean });

        modelBuilder.Entity<PublicationReportReason>()
            .HasKey(x => x.Id);

        modelBuilder.Entity<PublicationReportReason>()
            .Property(x => x.Id)
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<PublicationReportReason>()
            .HasIndex(x => x.Name)
            .IsUnique();

        modelBuilder.Entity<PublicationReport>()
            .HasOne(x => x.Publication)
            .WithMany(x => x.Reports)
            .HasForeignKey(x => x.PublicationId);

        modelBuilder.Entity<PublicationReport>()
            .HasOne(x => x.Reason)
            .WithMany(x => x.Reports)
            .HasForeignKey(x => x.ReasonId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PublicationReport>()
            .HasOne(x => x.ReporterUser)
            .WithMany(x => x.Reports)
            .HasForeignKey(x => x.ReporterUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PublicationReport>()
            .HasOne(x => x.ReviewedByUser)
            .WithMany()
            .HasForeignKey(x => x.ReviewedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PublicationReport>()
            .HasIndex(x => new { x.PublicationId, x.ReporterUserId })
            .IsUnique();

        modelBuilder.Entity<FavoriteList>()
            .HasOne(x => x.User)
            .WithMany(x => x.FavoriteLists)
            .HasForeignKey(x => x.UserId);

        modelBuilder.Entity<FavoriteList>()
            .HasIndex(x => new { x.UserId, x.Name })
            .IsUnique();

        modelBuilder.Entity<FavoriteListItem>()
            .HasOne(x => x.FavoriteList)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.FavoriteListId);

        modelBuilder.Entity<FavoriteListItem>()
            .HasOne(x => x.Publication)
            .WithMany(x => x.FavoriteListItems)
            .HasForeignKey(x => x.PublicationId);

        modelBuilder.Entity<FavoriteListItem>()
            .HasIndex(x => new { x.FavoriteListId, x.PublicationId })
            .IsUnique();

        modelBuilder.Entity<SharedPublicationList>()
            .HasOne(x => x.User)
            .WithMany(x => x.SharedPublicationLists)
            .HasForeignKey(x => x.UserId);

        modelBuilder.Entity<SharedPublicationList>()
            .HasIndex(x => new { x.UserId, x.Name });

        modelBuilder.Entity<SharedPublicationList>()
            .Property(x => x.DefaultMode)
            .HasMaxLength(20)
            .HasDefaultValue("Galeria");

        modelBuilder.Entity<SharedPublicationList>()
            .HasIndex(x => x.Slug)
            .IsUnique();

        modelBuilder.Entity<SharedPublicationListItem>()
            .HasOne(x => x.SharedPublicationList)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.SharedPublicationListId)
            .HasConstraintName("FK_SharedListItems_List");

        modelBuilder.Entity<SharedPublicationListItem>()
            .HasOne(x => x.Publication)
            .WithMany(x => x.SharedPublicationListItems)
            .HasForeignKey(x => x.PublicationId);

        modelBuilder.Entity<SharedPublicationListItem>()
            .HasIndex(x => new { x.SharedPublicationListId, x.PublicationId })
            .IsUnique()
            .HasDatabaseName("IX_SharedListItems_List_Publication");

        modelBuilder.Entity<PublicationFavorite>()
            .HasOne(x => x.Publication)
            .WithMany(x => x.Favorites)
            .HasForeignKey(x => x.PublicationId);

        modelBuilder.Entity<PublicationFavorite>()
            .HasOne(x => x.User)
            .WithMany(x => x.PublicationFavorites)
            .HasForeignKey(x => x.UserId);

        modelBuilder.Entity<PublicationFavorite>()
            .HasIndex(x => new { x.PublicationId, x.UserId })
            .IsUnique();

        modelBuilder.Entity<PublicationFavorite>()
            .HasIndex(x => x.CreatedAtUtc);

        modelBuilder.Entity<PublicationView>()
            .HasOne(x => x.Publication)
            .WithMany(x => x.Views)
            .HasForeignKey(x => x.PublicationId);

        modelBuilder.Entity<PublicationView>()
            .HasOne(x => x.ViewerUser)
            .WithMany(x => x.PublicationViews)
            .HasForeignKey(x => x.ViewerUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<PublicationView>()
            .HasIndex(x => new { x.PublicationId, x.ViewerUserId })
            .IsUnique();

        modelBuilder.Entity<PublicationView>()
            .HasIndex(x => new { x.PublicationId, x.AnonymousFingerprint })
            .IsUnique();

        modelBuilder.Entity<PublicationView>()
            .HasIndex(x => x.CreatedAtUtc);

        modelBuilder.Entity<SiteSuggestion>()
            .HasOne(x => x.User)
            .WithMany(x => x.SiteSuggestions)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<SiteSuggestion>()
            .HasIndex(x => x.CreatedAtUtc);

        modelBuilder.Entity<VentagramParameter>()
            .HasIndex(x => x.Key)
            .IsUnique();

        modelBuilder.Entity<VentagramParameter>()
            .HasOne(x => x.UpdatedByUser)
            .WithMany()
            .HasForeignKey(x => x.UpdatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<VerifiedOperation>()
            .HasOne(x => x.Publication)
            .WithMany(x => x.VerifiedOperations)
            .HasForeignKey(x => x.PublicationId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<VerifiedOperation>()
            .Property(x => x.OperationType)
            .HasConversion<byte?>()
            .HasColumnType("tinyint unsigned");

        modelBuilder.Entity<VerifiedOperation>()
            .HasOne(x => x.AdvertiserUser)
            .WithMany(x => x.OperationsAsAdvertiser)
            .HasForeignKey(x => x.AdvertiserUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<VerifiedOperation>()
            .HasOne(x => x.CounterpartyUser)
            .WithMany(x => x.OperationsAsCounterparty)
            .HasForeignKey(x => x.CounterpartyUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<VerifiedOperation>()
            .HasIndex(x => x.ResponseTokenHash)
            .IsUnique();

        modelBuilder.Entity<VerifiedOperation>()
            .HasIndex(x => new { x.PublicationId, x.Status });

        modelBuilder.Entity<VerifiedOperation>()
            .HasIndex(x => new { x.CounterpartyEmail, x.Status });

        modelBuilder.Entity<OperationReview>()
            .HasOne(x => x.VerifiedOperation)
            .WithMany(x => x.Reviews)
            .HasForeignKey(x => x.VerifiedOperationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<OperationReview>()
            .HasOne(x => x.ReviewerUser)
            .WithMany(x => x.ReviewsWritten)
            .HasForeignKey(x => x.ReviewerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<OperationReview>()
            .HasOne(x => x.ReviewedUser)
            .WithMany(x => x.ReviewsReceived)
            .HasForeignKey(x => x.ReviewedUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<OperationReview>()
            .HasIndex(x => new { x.VerifiedOperationId, x.ReviewerUserId })
            .IsUnique();

        modelBuilder.Entity<OperationReview>()
            .HasIndex(x => new { x.ReviewedUserId, x.ReviewedRole, x.SubmittedAtUtc });

    }
}
