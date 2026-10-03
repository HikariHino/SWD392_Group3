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
    private readonly IPasswordHasher _passwordHasher;

    public UserService(IUnitOfWork unitOfWork, IMapper mapper, IPasswordHasher passwordHasher)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _passwordHasher = passwordHasher;
    }

    public async Task<UserDto> GetUserByIdAsync(Guid id)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(id);
        if (user == null) throw new Application.Exceptions.NotFoundException("User not found");
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
        if (existingUser != null) throw new Application.Exceptions.ConflictException("Username already exists");

        var user = _mapper.Map<User>(dto);
        user.CreatedAt = DateTime.UtcNow;
        
        // C04: Hash passwords through application abstraction
        user.PasswordHash = _passwordHasher.HashPassword(dto.Password);

        await _unitOfWork.Users.AddAsync(user);
        await _unitOfWork.SaveChangesAsync();

        return _mapper.Map<UserDto>(user);
    }

    public async Task UpdateUserAsync(Guid id, UpdateUserDto dto)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(id);
        if (user == null) throw new Application.Exceptions.NotFoundException("User not found");

        if (!string.IsNullOrEmpty(dto.FullName)) user.FullName = dto.FullName;
        if (!string.IsNullOrEmpty(dto.Role)) user.Role = dto.Role;

        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task DeleteUserAsync(Guid id)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(id);
        if (user == null) throw new Application.Exceptions.NotFoundException("User not found");

        _unitOfWork.Users.Delete(user);
        await _unitOfWork.SaveChangesAsync();
    }
}
