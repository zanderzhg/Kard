﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Kard.Core.Dtos;
using Kard.Core.Entities;
using Kard.Core.IRepositories;
using Kard.Extensions;
using Kard.Json;
using Kard.Runtime.Security;
using Kard.Runtime.Security.Authentication.WeChat;
using Kard.Runtime.Session;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Senparc.Weixin.MP.Containers;
using Senparc.Weixin.MP.Helpers;

namespace Kard.Web.Controllers
{
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
    [Authorize(AuthenticationSchemes = WeChatAppDefaults.AuthenticationScheme)]
    [Produces("application/json")]
    [Route("essay")]
    public class EssayController : BaseController
    {
        private readonly IHostingEnvironment _env;
        private readonly IDefaultRepository _defaultRepository;
        private readonly IConfiguration _configuration;

        public EssayController(IHostingEnvironment env,
            ILogger<EssayController> logger,
            IMemoryCache memoryCache,
            IDefaultRepository defaultRepository,
            IKardSession kardSession,
            IConfiguration configuration) : base(logger, memoryCache, kardSession)
        {
            _env = env;
            _defaultRepository = defaultRepository;
            _configuration = configuration;
        }

        #region essay
        /// <summary>
        /// 获取单品信息
        /// </summary>
        /// <returns></returns>
        [AllowAnonymous]
        [HttpGet("{id}")]
        public ResultDto GetInfo(long id)
        {
            //单品信息
            var essayEntity = _defaultRepository.GetEssay(id, _kardSession.UserId);
            var resultDto = new ResultDto();
            resultDto.Result = essayEntity != null;
            resultDto.Data = essayEntity;
            //评论 喜欢

            //增加阅读数
            Task.Run(() =>
            {
                var result = _defaultRepository.UpdateBrowseNum(id);
                if (!result)
                {
                    _logger.LogError($"统计：单品{id}增加阅读数失败");
                }
            });

            return resultDto;
        }

        /// <summary>
        /// 上传文件
        /// </summary>
        /// <returns></returns>
        [HttpPost("uploadmedia")]
        //[Consumes("multipart/form-data")]
        //[RequestSizeLimit(100_000_000)]
        public ResultDto UploadMedia(IFormFile mediaFlie)
        {
            var result = new ResultDto();
            if (mediaFlie == null && Request.Form.Files.Any())
            {
                mediaFlie = Request.Form.Files[0];
            }
            if (mediaFlie == null)
            {
                result.Result = true;
                result.Message = "未选择文件";
                return result;
            }

            var now = DateTime.Now;
            string webRootPath = _env.WebRootPath;

            string newFolder = Path.Combine("user", _kardSession.UserId.ToString(), "media", now.ToString("yyyyMMdd"));
            string newPath = Path.Combine(webRootPath, newFolder);
            if (!Directory.Exists(newPath))
            {
                Directory.CreateDirectory(newPath);
            }

            if (mediaFlie.Length > 0)
            {
                string fileName = now.ToString("ddHHmmssffff");
                string fileExtension = Path.GetExtension(mediaFlie.FileName.Trim('"')).ToLower(); // Path.GetExtension(ContentDispositionHeaderValue.Parse(mediaFlie.ContentDisposition).FileName.Trim('"'));
                string fullPath = Path.Combine(newPath, fileName + fileExtension);
                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    mediaFlie.CopyTo(stream);
                }
                result.Result = true;
                result.Data = new { FileUrl = Path.Combine(newFolder, fileName).Replace("\\", "/"), FileExtension = fileExtension.Replace(".", "") };
                return result;
            }

            result.Result = false;
            result.Message = "上传失败";
            return result;
        }


        /// <summary>
        /// 添加纪录
        /// </summary>
        /// <param name="essayEntity"></param>
        /// <param name="mediaList"></param>
        [HttpPost("add")]
        public ResultDto AddEssay(EssayEntity essayEntity, IEnumerable<MediaEntity> mediaList)
        {
            /*private static readonly Regex _regex = new Regex(@"(?'group1'#)([^#]+?)(?'-group1'#)");
             if ((!this.EssayContent.IsNullOrEmpty()) && _regex.IsMatch(this.EssayContent))
                {
                    var matchCollection = _regex.Matches(this.EssayContent);
                    return matchCollection.First()?.Value.Replace("#","");
                }*/

            var createUserId = _kardSession.UserId.Value;
            IEnumerable<TagEntity> tagList = new List<TagEntity>();
            if (essayEntity.Content.Contains('#'))
            {
                var contentList = essayEntity.Content.Split('#');
                int contentListLastIndex = contentList.Length - 1;
                tagList = contentList.Where((item, index) => ((!string.IsNullOrEmpty(item)) && (index != contentListLastIndex))).Select((item, index) => { var tagEntity = new TagEntity { Sort = (index + 1), TagName = item }; tagEntity.AuditCreation(createUserId); return tagEntity; });
                essayEntity.Content = contentList.Last();
            }
            essayEntity.Location = "广州";
            essayEntity.AuditCreation(createUserId);
            mediaList.AuditCreation(createUserId);
            var result = _defaultRepository.AddEssay(essayEntity, tagList, mediaList);

            if (result)
            {
                string cacheKey = $"homeCover[{DateTime.Now.ToString("yyyyMMdd")}]";
                _memoryCache.Remove(cacheKey);
            }
            return new ResultDto { Result = result };
        }

        /// <summary>
        /// 相似列表
        /// </summary>
        /// <returns></returns>
        [AllowAnonymous]
        [HttpGet("similarlist")]
        public ResultDto<IEnumerable<EssayEntity>> GetEssaySimilarList(long essayId)
        {
            var resultDto = new ResultDto<IEnumerable<EssayEntity>>();
            resultDto.Result = true;
            resultDto.Data = _defaultRepository.GetEssaySimilarList(essayId);
            return resultDto;
        }


        /// <summary>
        /// 其他列表
        /// </summary>
        /// <returns></returns>
        [AllowAnonymous]
        [HttpGet("otherlist")]
        public ResultDto<IEnumerable<EssayEntity>> GetEssayOtherList(long essayId)
        {
            var resultDto = new ResultDto<IEnumerable<EssayEntity>>();
            resultDto.Result = true;
            resultDto.Data = _defaultRepository.GetEssayOtherList(essayId);
            return resultDto;
        }

        #endregion


        #region other
        /// <summary>
        /// 添加喜欢
        /// </summary>
        /// <param name="essayId"></param>
        /// <returns></returns>
        [HttpPost("like")]
        public ResultDto Like(long essayId)
        {
            return _defaultRepository.ChangeEssayLike(_kardSession.UserId.Value, essayId);
        }

        /// <summary>
        /// 喜欢列表
        /// </summary>
        /// <param name="essayId"></param>
        /// <returns></returns>
        [AllowAnonymous]
        [HttpGet("likelist")]
        public ResultDto<IEnumerable<EssayLikeEntity>> GetLikeList(long essayId)
        {
            var resultDto = new ResultDto<IEnumerable<EssayLikeEntity>>();
            resultDto.Result = true;
            resultDto.Data = _defaultRepository.GetEssayLikeList(essayId);
            return resultDto;
        }

        /// <summary>
        /// 添加评论
        /// </summary>
        /// <param name="essayId"></param>
        /// <param name="content"></param>
        /// <param name="parentId"></param>
        /// <returns></returns>
        [HttpPost("addcomment")]
        public ResultDto AddComment(long essayId, string content, long? parentId)
        {
            var resultDto = new ResultDto();
            var essayComment = new EssayCommentEntity
            {
                EssayId = essayId,
                Content = content,
                ParentId = parentId
            };

            essayComment.AuditCreation(_kardSession.UserId.Value);
            resultDto.Result = _defaultRepository.CreateAndGetId<EssayCommentEntity, long>(essayComment).Result;
            return resultDto;
        }

        /// <summary>
        /// 评论列表
        /// </summary>
        /// <param name="essayId"></param>
        /// <returns></returns>
        [AllowAnonymous]
        [HttpGet("commentlist")]
        public ResultDto<IEnumerable<EssayCommentDto>> GetCommentList(long essayId)
        {
            var essayCommentList = _defaultRepository.GetEssayCommentList(essayId) ?? new List<EssayCommentDto>();
            var pageCommentList = essayCommentList.Where((item, index) => index < 10);

            var resultDto = new ResultDto<IEnumerable<EssayCommentDto>>();
            resultDto.Result = true;
            resultDto.Data = AppendChild(pageCommentList, essayCommentList);
            return resultDto;
        }

        private IEnumerable<EssayCommentDto> AppendChild(IEnumerable<EssayCommentDto> childList, IEnumerable<EssayCommentDto> commentList)
        {
            foreach (var child in childList)
            {
                if (child.ParentId != null && child.ParentId.HasValue)
                {
                    child.ParentCommentDtoList = commentList.Where(c => c.Id == child.ParentId);
                    child.ParentCommentDtoList = AppendChild(child.ParentCommentDtoList, commentList);
                }
            }
            return childList;
        }


        /// <summary>
        /// 微信转发
        /// </summary>
        [AllowAnonymous]
        [HttpPost("jssdk")]
        public async Task<ResultDto> JsSdkAsync(string url)
        {
            var configSection = _configuration.GetSection("WeChat:Web");
            //获取时间戳
            var timestamp = JSSDKHelper.GetTimestamp();
            //获取随机码
            var nonceStr = JSSDKHelper.GetNoncestr();
            var appId = configSection.GetValue<string>("AppId");
            var appSecret = configSection.GetValue<string>("AppSecret");
            //获取票证

            var jsTicket = await JsApiTicketContainer.TryGetJsApiTicketAsync(appId, appSecret);
            //获取签名
            //nonceStr = "Wm3WZYTPz0wzccnW";
            //jsTicket = "sM4AOVdWfPE4DxkXGEs8VMCPGGVi4C3VM0P37wVUCFvkVAy_90u5h9nbSlYy3-Sl-HhTdfl2fzFy1AOcHKP7qg";
            //timestamp = "1414587457";
            //url = "http://mp.weixin.qq.com?params=value";
            //url = url?? Request.GetAbsoluteUri();
            var signature = JSSDKHelper.GetSignature(jsTicket, nonceStr, timestamp, url);

            var resultDto = new ResultDto
            {
                Result = true,
                Data = new { appId, timestamp, nonceStr, signature },
                Message = "查询成功"
            };

            _logger.LogDebug(Serialize.ToJson(new { url, appId, jsTicket, timestamp, nonceStr, signature }));
            return resultDto;
        }





        /// <summary>
        /// 测试
        /// </summary>
        /// <param name="connNum"></param>
        /// <returns></returns>
        [HttpGet("test")]
        public async Task<ResultDto<string>> TestAsync(long? connNum = 10)
        {
            var milliseconds = await RunTask(connNum);
            _logger.LogDebug($"耗时：{milliseconds}ms");
            return new ResultDto<string>() { Result = true, Data = $"耗时：{milliseconds}ms" };
        }

        private async Task<long> RunTask(long? taskNum)
        {
            var taskList = new List<Task<long>>();
            for (int i = 0; i < taskNum; i++)
            {
                taskList.Add(Task.Run(() =>
                {
                    Stopwatch sw = new Stopwatch();
                    sw.Start();
                    for (int j = 0; j < taskNum; j++)
                    {
                        Like(taskNum.Value);
                    }
                    sw.Stop();
                    return sw.ElapsedMilliseconds;
                }));
            }

            var result = await Task.WhenAll(taskList);
            return (result.Sum() / (taskNum.Value * taskNum.Value));
        }


        ///// <summary>
        ///// 微信参数准备
        ///// </summary>
        //[HttpPost("jssdk")]
        //public async Task<object> JsSdkAsync(string url)
        //{
        //    //获取时间戳
        //    var timestamp =GetTimestamp();
        //    //获取随机码
        //    var nonceStr =  EncryptHelper.GetMD5(Guid.NewGuid().ToString(), "UTF-8");
        //    var appId = "wx109fc14b4956fc70";
        //    var appSecret = "a8e7f19d69cbde0272fd866fe7392874";
        //    //获取票证

        //    var jsTicket = await JsApiTicketContainer.TryGetJsApiTicketAsync(appId, appSecret);
        //    //获取签名
        //    //nonceStr = "Wm3WZYTPz0wzccnW";
        //    //jsTicket = "sM4AOVdWfPE4DxkXGEs8VMCPGGVi4C3VM0P37wVUCFvkVAy_90u5h9nbSlYy3-Sl-HhTdfl2fzFy1AOcHKP7qg";
        //    //timestamp = "1414587457";
        //    //url = "http://mp.weixin.qq.com?params=value";
        //    //url = url?? Request.GetAbsoluteUri();
        //    var signature = JSSDKHelper.GetSignature(jsTicket, nonceStr, timestamp, url);


        //    _logger.LogDebug(Serialize.ToJson(new { url, appId, jsTicket, timestamp, nonceStr, signature }));
        //    return json.ToJsonResult();
        //}


        //#endregion


        //public static async Task<AccessTokenResult> GetTokenAsync(string appid, string secret, string grant_type = "client_credential")
        //{
        //    //注意：此方法不能再使用ApiHandlerWapper.TryCommonApi()，否则会循环
        //    var url = string.Format(Config.ApiMpHost + "/cgi-bin/token?grant_type={0}&appid={1}&secret={2}",
        //                            grant_type.AsUrlData(), appid.AsUrlData(), secret.AsUrlData());

        //    AccessTokenResult result = await Get.GetJsonAsync<AccessTokenResult>(url);
        //    return result;
        //}

        //public readonly static DateTime BaseTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);//Unix起始时间

        //public static string GetTimestamp()
        //{
        //    //var ts = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0);
        //    //return Convert.ToInt64(ts.TotalSeconds).ToString();
        //    var ts = GetWeixinDateTime(DateTime.Now);
        //    return ts.ToString();
        //}

        //public static long GetWeixinDateTime(DateTime dateTime)
        //{
        //    return (long)(dateTime.ToUniversalTime() - BaseTime).TotalSeconds;
        //}

        #endregion

    }

}