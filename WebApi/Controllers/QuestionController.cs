using Microsoft.AspNetCore.Mvc;
using Application.Interfaces.Services;
using System.Threading.Tasks;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QuestionController : ControllerBase
    {
        private readonly IQuestionBankService _questionBankService;

        public QuestionController(IQuestionBankService questionBankService)
        {
            _questionBankService = questionBankService;
        }

        [HttpPost("import")]
        public async Task<IActionResult> ImportQuestions()
        {
            var result = await _questionBankService.ImportQuestionsAsync("dummyPath");
            return Ok(result);
        }
    }
}
