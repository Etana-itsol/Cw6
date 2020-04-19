using Cw5.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Cw5.DTOs.Requests;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using Cw5.DTOs.Responses;
using System.Globalization;

namespace Cw5.Services
{
    public class SqlServerStudentDbServices : IStudentDbServices
    {
        /*
        public Enrollment EnrollStudent(EnrollStudentRequest request)
        {
            using (var con = new SqlConnection("Data Source=db-mssql;Initial Catalog=s18725;Integrated Security=True"))
            using (var com = new SqlCommand())
            {
                com.Connection = con;
                con.Open();
                var tran = con.BeginTransaction();
                try
                {
                    com.CommandText = "Exec EnrollStudent @IndexNumber, @FirstName, @LastName, @BirthDate, @Studies";
                    com.Parameters.AddWithValue("IndexNumber", request.IndexNumber);
                    com.Parameters.AddWithValue("FirstName", request.FirstName);
                    com.Parameters.AddWithValue("LastName", request.LastName);
                    com.Parameters.AddWithValue("BirthDate", request.Birthdate);
                    com.Parameters.AddWithValue("Studies", request.Studies);
                }
                catch 
                {
                    tran.Rollback();
                }
                tran.Commit();
                com.ExecuteNonQuery();

            }
            return new Enrollment() { Studies = request.Studies, Semester = 1, StartDate = DateTime.Today };
        }
        */
        public Enrollment EnrollStudent(EnrollStudentRequest request)
        {
            using var con = new SqlConnection("Data Source=db-mssql;Initial Catalog=s18725;Integrated Security=True");
            con.Open();
            using var transaction = con.BeginTransaction();

            //check if studies exists
            if (!CheckStudies(request.Studies, con, transaction))
            {
                transaction.Rollback();
                throw new Exception ( "Studies does not exist.");
            }

            //get (or create and get) the enrollment
            var enrollment = NewEnrollment(request.Studies, 1, con, transaction);
            if (enrollment == null)
            {
                CreateEnrollment(request.Studies, 1, DateTime.Now, con, transaction);
                enrollment = NewEnrollment(request.Studies, 1, con, transaction);
            }

            //check if provided index number is unique
            if (GetStudent(request.IndexNumber) != null)
            {
                transaction.Rollback();
                throw new Exception( $"Index number ({request.IndexNumber}) is not unique.");
            }

            //create a student and commit the transaction
            CreateStudent(request.IndexNumber, request.FirstName, request.LastName, request.BirthDate, enrollment.IdEnrollment, con, transaction);
            transaction.Commit();

            //return Enrollment object
            return enrollment;
        }
    
        public PromoteStudentRequest PromoteStudents(PromoteStudentRequest request)
        {
            using (var con = new SqlConnection("Data Source=db-mssql;Initial Catalog=s18725;Integrated Security=True"))
            using (var com = new SqlCommand("Execute PromoteStudents @name, @semester;", con))
            {
                Console.WriteLine("open");
                con.Open();

                var tran = con.BeginTransaction();
                try 
                {
                    Console.WriteLine("try");
            
                    com.Parameters.AddWithValue("name", request.Studies);
                    com.Parameters.AddWithValue("semester", request.Semester);

                    tran.Commit();
                    com.ExecuteNonQuery();
                }
                catch (SqlException exc)
                {
                    tran.Rollback();
                    Console.WriteLine("err");         
                }
            }
           return request;
        }

        public Student GetStudent(string id)
        {

            using (SqlConnection con = new SqlConnection("Data Source=db-mssql;Initial Catalog=s18725;Integrated Security=True"))
            using (SqlCommand com = new SqlCommand())
            {
                com.Connection = con;
                com.CommandText = "SELECT IndexNumber,FirstName,LastName,BirthDate,Name,Semester FROM Student S JOIN Enrollment E on S.IdEnrollment = E.IdEnrollment JOIN Studies St on E.IdStudy = St.IdStudy WHERE IndexNumber = @index";
                com.Parameters.AddWithValue("index", id);

                con.Open();
                var dr = com.ExecuteReader();
                if (dr.Read())
                {
                    Student st = new Student
                    {
                        IndexNumber = dr["IndexNumber"].ToString(),
                        FirstName = dr["FirstName"].ToString(),
                        LastName = dr["LastName"].ToString(),
                        BirthDate = DateTime.Parse(dr["BirthDate"].ToString()),
                        Studies = dr["Name"].ToString(),
                        Semester = int.Parse(dr["Semester"].ToString()),
                    };
                    return st;
                }
                return null;
            }
        }
        public IEnumerable<Student> GetStudents() 
        {
            var list = new List<Student>();

            using (SqlConnection con = new SqlConnection("Data Source=db-mssql;Initial Catalog=s18725;Integrated Security=True"))
            using (SqlCommand com = new SqlCommand())
            {
                com.Connection = con;
                com.CommandText = "select IndexNumber,FirstName,LastName,BirthDate ,Name,Semester from Student s join Enrollment e on e.IdEnrollment=s.IdEnrollment join Studies st on st.IdStudy=e.IdStudy";

                con.Open();
                SqlDataReader dr = com.ExecuteReader();

                while (dr.Read())
                {
                    var st = new Student();
                    st.IndexNumber = dr["IndexNumber"].ToString();
                    st.FirstName = dr["FirstName"].ToString();
                    st.LastName = dr["LastName"].ToString();
                    st.BirthDate = DateTime.Parse(dr["BirthDate"].ToString());
                    //st.BirthDate = DateTime.Now;
                    st.Studies = dr["Name"].ToString();
                    st.Semester = int.Parse(dr["Semester"].ToString());
                    list.Add(st);
                }
                con.Dispose();
            }
            return list;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////
        private bool CheckStudies(string name, SqlConnection con, SqlTransaction transaction) 
        {
            using var cmd = new SqlCommand
            {
                Connection = con,
                Transaction = transaction,
                CommandText = @"SELECT 1 from Studies s WHERE s.Name = @name;"
            };
            cmd.Parameters.AddWithValue("name", name);
            using var dr = cmd.ExecuteReader();
            return dr.Read();
        }
        private Enrollment NewEnrollment(string studiesName, int semester, SqlConnection con, SqlTransaction transaction)
        {
            using var cmd = new SqlCommand
            {
                Connection = con,
                Transaction = transaction,
                CommandText = @"SELECT TOP 1 e.IdEnrollment, e.IdStudy, e.StartDate
                                FROM Enrollment e JOIN Studies s ON e.IdStudy=s.IdStudy
                                WHERE e.Semester = @Semester AND s.Name = @Name
                                ORDER BY IdEnrollment DESC;"
            };

            cmd.Parameters.AddWithValue("Name", studiesName);
            cmd.Parameters.AddWithValue("Semester", semester);

            using var dr = cmd.ExecuteReader();
            if (dr.Read())
            {
                return new Enrollment
                {
                    IdEnrollment = int.Parse(dr["IdEnrollment"].ToString()),
                    Semester = semester,
                    IdStudy = int.Parse(dr["IdStudy"].ToString()),
                    StartDate = DateTime.Parse(dr["StartDate"].ToString()),
                };
            }
            return null;
        }
        private void CreateEnrollment(string studiesName, int semester, DateTime startDate, SqlConnection con, SqlTransaction transaction)
        {
            using var cmd = new SqlCommand
            {
                Connection = con,
                Transaction = transaction,
                CommandText = @"INSERT INTO Enrollment(IdEnrollment, IdStudy, StartDate, Semester)
                                VALUES ((SELECT ISNULL(MAX(e.IdEnrollment)+1,1) FROM Enrollment e), 
		                                (SELECT s.IdStudy FROM Studies s WHERE s.Name = @Name), 
		                                @StartDate,
		                                @Semester);"
            };

            cmd.Parameters.AddWithValue("Name", studiesName);
            cmd.Parameters.AddWithValue("Semester", semester);
            cmd.Parameters.AddWithValue("StartDate", startDate);
            cmd.ExecuteNonQuery();
        }
        private void CreateStudent(string indexNumber, string firstName, string lastName, DateTime BirthDate, int idEnrollment, SqlConnection sqlConnection = null, SqlTransaction transaction = null)
        {
            using var cmd = new SqlCommand
            {
                CommandText = @"INSERT INTO Student(IndexNumber, FirstName, LastName, BirthDate, IdEnrollment)
                                VALUES (@IndexNumber, @FirstName, @LastName, @BirthDate, @IdEnrollment);"
            };
            cmd.Parameters.AddWithValue("IndexNumber", indexNumber);
            cmd.Parameters.AddWithValue("FirstName", firstName);
            cmd.Parameters.AddWithValue("LastName", lastName);
            cmd.Parameters.AddWithValue("BirthDate", BirthDate);
            cmd.Parameters.AddWithValue("IdEnrollment", idEnrollment);

            if (sqlConnection == null)
            {
                using var con = new SqlConnection("Data Source=db-mssql;Initial Catalog=s18725;Integrated Security=True");
                con.Open();
                cmd.Connection = con;
                cmd.ExecuteNonQuery();
            }
            else
            {
                cmd.Connection = sqlConnection;
                cmd.Transaction = transaction;
                cmd.ExecuteNonQuery();
            }
        }
    }
    
}
