using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Domain.Entities.QuestionBank;

namespace Infrastructure.Persistence.Configurations;

public class RubricConfiguration : IEntityTypeConfiguration<Rubric>
{
    public void Configure(EntityTypeBuilder<Rubric> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Criteria)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(r => r.Weight)
            .IsRequired();

        builder.Property(r => r.MaxScore)
            .IsRequired();

        builder.HasQueryFilter(r => !r.IsDeleted);
    }
}
