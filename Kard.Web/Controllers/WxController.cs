﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using Kard.Json;
using Kard.Core.Dtos;
using Microsoft.Extensions.Caching.Memory;
using Kard.Runtime.Session;
using Kard.Core.AppServices.Default;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using Kard.Core.Entities;
using Kard.Runtime.Security.Authentication.WeChat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Kard.Core.IRepositories;

namespace Kard.Web.Controllers
{
    [Authorize(AuthenticationSchemes = WeChatAppDefaults.AuthenticationScheme)]
    [Produces("application/json")]
    [Route("[controller]")]
    public class WxController : BaseController
    {
        private readonly ILoginAppService _loginAppService;
        private readonly IDefaultRepository _defaultRepository;

        public WxController(
          ILogger<WxController> logger,
          IMemoryCache memoryCache,
          ILoginAppService loginAppService,
          IKardSession kardSession,
          IDefaultRepository defaultRepository) : base(logger, memoryCache, kardSession)
        {
            _loginAppService = loginAppService;
            _defaultRepository = defaultRepository;
        }

        /// <summary>
        /// 微信登陆
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        [AllowAnonymous]
        [HttpGet("login")]
        public async Task<ResultDto> Login(string code)
        {
            var aliveResult = _loginAppService.WxLogin(code);
            if (aliveResult.Result)
            {
                var identity = aliveResult.Data;
                //HttpContext.Session.SetString("ssss", "hello world!");
                await HttpContext.SignOutAsync(WeChatAppDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(WeChatAppDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = true });
            }

            var result = new ResultDto { Result = aliveResult.Result, Message = aliveResult.Message };
            return result;
        }



        /// <summary>
        /// 微信注册
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        [HttpPost("register")]
        public ResultDto Register([FromBody]KuserEntity user)
        {
            //var ssss=HttpContext.Session.GetString("ssss");
            if (string.IsNullOrEmpty(_kardSession.WxOpenId))
            {
                return new ResultDto { Result = false, Message = "未找到open" };
            }
            user.WxOpenId = _kardSession.WxOpenId;

            var result = _loginAppService.Register("wxRegister", user);
            return result;
        }

        /// <summary>
        /// 添加任务
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        [HttpPost("addtask")]
        public ResultDto AddTask([FromBody]LongTaskEntity entity)
        {

            var result = new ResultDto();
            if (!entity.IsLongTerm)
            {
                var taskEntity= new TaskEntity
                {
                    TaskDate = entity.TaskDate,
                    StartTime = entity.StartTime,
                    EndTime = entity.EndTime,
                    Content = entity.Content,
                    IsRemind = entity.IsRemind,
                    IsDone=false
                };
                taskEntity.AuditCreation(_kardSession.UserId.Value);
                var createResult = _defaultRepository.CreateAndGetId<TaskEntity, long>(taskEntity);
                result.Result = createResult.Result;
                result.Message = createResult.Message;

                return result;
            }


            entity.AuditCreation(_kardSession.UserId.Value);
            result = _defaultRepository.AddTask(entity);

            return result;
        }

        /// <summary>
        /// 获取任务列表
        /// </summary>
        /// <returns></returns>
        [HttpGet("gettask")]
        public ResultDto<IEnumerable<TaskEntity>> GetTask()
        {
            var result = new ResultDto<IEnumerable<TaskEntity>>();
            result.Result =true;
            result.Data = _defaultRepository.QueryList<TaskEntity>("select * from task where CreatorUserId=@CreatorUserId and TaskDate=@TaskDate and IsDeleted=0 order by IsDone,StartTime", new { CreatorUserId = _kardSession.UserId,TaskDate = DateTime.Now.Date });
            return result;
        }
    }
}