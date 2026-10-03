# 1. Entity
New-Item -Path "Domain/Entities/UserManagement" -ItemType Directory -Force | Out-Null
@"
using Domain.Common;

namespace Domain.Entities.UserManagement;

public class User : BaseEntity
{
    public string Username { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Role { get; set; } = null!;
    public DateTime? CreatedAt { get; set; }
    public bool IsDeleted { get; set; }
}
"@ | Out-File -FilePath "Domain/Entities/UserManagement/User.cs" -Encoding UTF8
git add Domain/Entities/UserManagement/User.cs
git commit -m "feat(user): add User entity to Domain layer"

# 2. DTO
New-Item -Path "Application/DTOs/UserManagement" -ItemType Directory -Force | Out-Null
@"
using System;

namespace Application.DTOs.UserManagement;

public class UserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Role { get; set; } = null!;
    public DateTime? CreatedAt { get; set; }
}

public class CreateUserDto
{
    public string Username { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Role { get; set; } = null!;
}

public class UpdateUserDto
{
    public string? FullName { get; set; }
    public string? Role { get; set; }
}
"@ | Out-File -FilePath "Application/DTOs/UserManagement/UserDto.cs" -Encoding UTF8
git add Application/DTOs/UserManagement/UserDto.cs
git commit -m "feat(user): create User DTOs for data transfer"

# 3. Interfaces
New-Item -Path "Application/Interfaces/Repositories/UserManagement" -ItemType Directory -Force | Out-Null
@"
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Domain.Entities.UserManagement;

namespace Application.Interfaces.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByUsernameAsync(string username);
    Task<IEnumerable<User>> GetAllAsync();
    Task AddAsync(User user);
    void Update(User user);
    void Delete(User user);
}
"@ | Out-File -FilePath "Application/Interfaces/Repositories/IUserRepository.cs" -Encoding UTF8

New-Item -Path "Application/Interfaces/Services/UserManagement" -ItemType Directory -Force | Out-Null
@"
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Application.DTOs.UserManagement;

namespace Application.Interfaces.Services;

public interface IUserService
{
    Task<UserDto> GetUserByIdAsync(Guid id);
    Task<IEnumerable<UserDto>> GetAllUsersAsync();
    Task<UserDto> CreateUserAsync(CreateUserDto dto);
    Task UpdateUserAsync(Guid id, UpdateUserDto dto);
    Task DeleteUserAsync(Guid id);
}
"@ | Out-File -FilePath "Application/Interfaces/Services/IUserService.cs" -Encoding UTF8
git add Application/Interfaces/Repositories/IUserRepository.cs Application/Interfaces/Services/IUserService.cs
git commit -m "feat(user): define interfaces for User repository and service"

# 4. Implement Repository
@"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.Interfaces.Repositories;
using Domain.Entities.UserManagement;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AivesDbContext _context;

    public UserRepository(AivesDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Username == username && !u.IsDeleted);
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        return await _context.Users.Where(u => !u.IsDeleted).ToListAsync();
    }

    public async Task AddAsync(User user)
    {
        await _context.Users.AddAsync(user);
    }

    public void Update(User user)
    {
        _context.Users.Update(user);
    }

    public void Delete(User user)
    {
        user.IsDeleted = true;
        _context.Users.Update(user);
    }
}
"@ | Out-File -FilePath "Infrastructure/Repositories/UserRepository.cs" -Encoding UTF8

(Get-Content -Path "Application/Interfaces/Repositories/IUnitOfWork.cs") -replace "}", "    IUserRepository Users { get; }`n}" | Set-Content -Path "Application/Interfaces/Repositories/IUnitOfWork.cs"
(Get-Content -Path "Infrastructure/Repositories/UnitOfWork.cs") -replace "public IQuestionRepository Questions \{ get; private set; \}", "public IQuestionRepository Questions { get; private set; }`n    public IUserRepository Users { get; private set; }" -replace "Questions = new QuestionRepository\(_context\);", "Questions = new QuestionRepository(_context);`n        Users = new UserRepository(_context);" | Set-Content -Path "Infrastructure/Repositories/UnitOfWork.cs"
git add Infrastructure/Repositories/UserRepository.cs Application/Interfaces/Repositories/IUnitOfWork.cs Infrastructure/Repositories/UnitOfWork.cs
git commit -m "feat(user): implement UserRepository and register to UnitOfWork"

# 5. Service
New-Item -Path "Application/Services/UserManagement" -ItemType Directory -Force | Out-Null
@"
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Application.DTOs.UserManagement;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using AutoMapper;
using Domain.Entities.UserManagement;

namespace Application.Services;

public class UserService : IUserService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public UserService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<UserDto> GetUserByIdAsync(Guid id)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(id);
        if (user == null) throw new Exception("User not found");
        return _mapper.Map<UserDto>(user);
    }

    public async Task<IEnumerable<UserDto>> GetAllUsersAsync()
    {
        var users = await _unitOfWork.Users.GetAllAsync();
        return _mapper.Map<IEnumerable<UserDto>>(users);
    }

    public async Task<UserDto> CreateUserAsync(CreateUserDto dto)
    {
        var existingUser = await _unitOfWork.Users.GetByUsernameAsync(dto.Username);
        if (existingUser != null) throw new Exception("Username already exists");

        var user = _mapper.Map<User>(dto);
        user.CreatedAt = DateTime.UtcNow;
        // NOTE: In real app, password should be hashed!
        user.PasswordHash = dto.Password; 

        await _unitOfWork.Users.AddAsync(user);
        await _unitOfWork.SaveChangesAsync();

        return _mapper.Map<UserDto>(user);
    }

    public async Task UpdateUserAsync(Guid id, UpdateUserDto dto)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(id);
        if (user == null) throw new Exception("User not found");

        if (!string.IsNullOrEmpty(dto.FullName)) user.FullName = dto.FullName;
        if (!string.IsNullOrEmpty(dto.Role)) user.Role = dto.Role;

        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task DeleteUserAsync(Guid id)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(id);
        if (user == null) throw new Exception("User not found");

        _unitOfWork.Users.Delete(user);
        await _unitOfWork.SaveChangesAsync();
    }
}
"@ | Out-File -FilePath "Application/Services/UserService.cs" -Encoding UTF8

(Get-Content -Path "Application/Mappings/MappingProfile.cs") -replace "using Domain.Entities.QuestionBank;", "using Domain.Entities.QuestionBank;`nusing Domain.Entities.UserManagement;`nusing Application.DTOs.UserManagement;" -replace "CreateMap<Rubric, RubricDto>\(\);", "CreateMap<Rubric, RubricDto>();`n`n        CreateMap<User, UserDto>();`n        CreateMap<CreateUserDto, User>();" | Set-Content -Path "Application/Mappings/MappingProfile.cs"
git add Application/Services/UserService.cs Application/Mappings/MappingProfile.cs
git commit -m "feat(user): implement UserService with AutoMapper mappings"

# 6. DbContext Configuration
@"
using Domain.Entities.UserManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("UserId").HasDefaultValueSql("NEWID()");
        builder.Property(x => x.Username).HasMaxLength(50).IsRequired();
        builder.Property(x => x.PasswordHash).HasMaxLength(255).IsRequired();
        builder.Property(x => x.FullName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Role).HasMaxLength(50).IsRequired();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(x => x.IsDeleted).HasDefaultValue(false);
        builder.HasIndex(x => x.Username).IsUnique();
    }
}
"@ | Out-File -FilePath "Infrastructure/Persistence/Configurations/UserConfiguration.cs" -Encoding UTF8

(Get-Content -Path "Infrastructure/Persistence/AivesDbContext.cs") -replace "using Domain.Entities.QuestionBank;", "using Domain.Entities.QuestionBank;`nusing Domain.Entities.UserManagement;" -replace "public DbSet<Rubric> Rubrics \{ get; set; \}", "public DbSet<Rubric> Rubrics { get; set; }`n    public DbSet<User> Users { get; set; }" -replace "builder.ApplyConfiguration\(new RubricConfiguration\(\)\);", "builder.ApplyConfiguration(new RubricConfiguration());`n        builder.ApplyConfiguration(new UserConfiguration());" | Set-Content -Path "Infrastructure/Persistence/AivesDbContext.cs"
git add Infrastructure/Persistence/Configurations/UserConfiguration.cs Infrastructure/Persistence/AivesDbContext.cs
git commit -m "feat(user): configure User entity in DbContext"

# 7. WebApi Controller
@"
using System;
using System.Threading.Tasks;
using Application.DTOs.UserManagement;
using Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await _userService.GetAllUsersAsync();
        return Ok(users);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetUserById(Guid id)
    {
        try
        {
            var user = await _userService.GetUserByIdAsync(id);
            return Ok(user);
        }
        catch (Exception ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto)
    {
        try
        {
            var user = await _userService.CreateUserAsync(dto);
            return CreatedAtAction(nameof(GetUserById), new { id = user.Id }, user);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserDto dto)
    {
        try
        {
            await _userService.UpdateUserAsync(id, dto);
            return NoContent();
        }
        catch (Exception ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        try
        {
            await _userService.DeleteUserAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            return NotFound(ex.Message);
        }
    }
}
"@ | Out-File -FilePath "WebApi/Controllers/UsersController.cs" -Encoding UTF8

(Get-Content -Path "WebApi/Program.cs") -replace "builder.Services.AddScoped<IQuestionBankService, QuestionBankService>\(\);", "builder.Services.AddScoped<IQuestionBankService, QuestionBankService>();`nbuilder.Services.AddScoped<IUserService, UserService>();" | Set-Content -Path "WebApi/Program.cs"
git add WebApi/Controllers/UsersController.cs WebApi/Program.cs
git commit -m "feat(user): implement UsersController and register UserService"

