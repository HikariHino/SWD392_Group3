using AutoMapper;
using Domain.Entities.QuestionBank;
using Domain.Entities.UserManagement;
using Application.DTOs.UserManagement;
using Application.DTOs.QuestionBank;

namespace Application.Mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // Question Mappings
            CreateMap<Question, QuestionDto>()
                .ForMember(dest => dest.CourseCode, opt => opt.MapFrom(src => src.Course != null ? src.Course.Code : null))
                .ForMember(dest => dest.CourseName, opt => opt.MapFrom(src => src.Course != null ? src.Course.Name : null))
                .ForMember(dest => dest.Rubrics, opt => opt.MapFrom(src => src.Rubrics.Where(r => !r.IsDeleted)));

            CreateMap<CreateQuestionRequest, Question>()
                .ForMember(dest => dest.Rubrics, opt => opt.MapFrom(src => src.Rubrics));

            // Rubric Mappings
            CreateMap<Rubric, RubricDto>();

        CreateMap<User, UserDto>();
        CreateMap<CreateUserDto, User>();
            CreateMap<CreateRubricRequest, Rubric>();
        }
    }
}
