using FilmrecAPI.bzl;
using FilmrecAPI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FilmrecAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RecommenderController : ControllerBase
    {
        private readonly IRecommenderBzl _recommenderBzl;
        public RecommenderController(IRecommenderBzl recommenderBzl)
        {
            _recommenderBzl = recommenderBzl;
        }
        [HttpPost]
        public Task<RecommenderResult> recommendMedia([FromBody] RecommenderContext recommenderContext)
        {
            return _recommenderBzl.recommendMedia(recommenderContext);
        }
    }
}
