using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cw5.Models;
using Cw5.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Cw5.Controllers
{
    [Route("api/students")]
    [ApiController]
    public class StudentsController : ControllerBase
    {
        private IStudentDbServices _db;

        public StudentsController(IStudentDbServices db)
        {
            _db = db;
        }

        [HttpGet]
        public IEnumerable<Student> GetStudents()
        {
            var response = _db.GetStudents();
            return response;
        }

        [HttpGet("{indexNumber}")]
        public Student GetStudent(string id){
            var response = _db.GetStudent(id);
            return response;
        }
    }
}