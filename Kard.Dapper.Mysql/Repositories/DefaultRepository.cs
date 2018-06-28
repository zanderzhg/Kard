﻿using Dapper;
using Kard.Core.Dtos;
using Kard.Core.Entities;
using Kard.Core.IRepositories;
using Kard.Runtime.Session;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using DapperExtensionsCore;

namespace Kard.Dapper.Mysql.Repositories
{
    public class DefaultRepository : Repository, IDefaultRepository
    {


        public DefaultRepository( IConfiguration configuration, ILogger<DefaultRepository> logger) : base( configuration, logger)
        {
        }

        public CoverEntity GetDateCover(DateTime showDate)
        {
            return ConnExecute(conn =>
            {
                string sql = @"select 
                                cover.*,
	                            media.*,
                                essay.*,
		                        kuser.*   
		                        from 
		                        (select * from cover where showdate <= @ShowDate order by showdate desc limit 1 ) cover
		                        left join media on cover.mediaid = media.id   
		                        left join essay on media.essayid = essay.id 
                                left join kuser on essay.creatoruserid=kuser.id 
                                where essay.isdeleted=0 and media.mediatype='picture'  ";
                var entityList = conn.Query<CoverEntity, MediaEntity, EssayEntity, KuserEntity, CoverEntity>(sql, (cover, media, essay, kuser) =>
                  {
                      media.Essay = essay;
                      media.Kuser = kuser;
                      cover.Media = media;
                      return cover;
                  },
                  new { ShowDate = showDate },
                  splitOn: "Id");

                if (entityList != null && entityList.Any())
                {
                    return entityList.First();
                }

                return null;
            });
        }




        public IEnumerable<TopMediaDto> GetHomeMediaPictureList(int count, string type)
        {
            string sql = string.Empty;

            var param = new object();
            switch (type)
            {
                case "热门单品":
                    sql = @"select essay.Id,essay.Category,essay.ShareNum,essay.LikeNum,essay.BrowseNum,essay.CommentNum,essay.title,essay.Location,essay.CreatorUserId,essay.CreationTime,kuser.AvatarUrl,kuser.NickName CreatorNickName,t2.MediaCount,t2.CdnPath,t2.MediaExtension,tag.*  from 
                (
                select t.EssayId,t.CdnPath,t.MediaExtension,count(media.Id) MediaCount
                from (
                select media.EssayId,media.CdnPath,media.MediaExtension from media join essay on media.EssayId=essay.Id  
               where   media.Sort=1 and media.MediaType='picture' and media.CreationTime>=@CreationTime order by (essay.LikeNum+essay.ShareNum+essay.BrowseNum+essay.CommentNum) desc,essay.CreationTime desc limit @Count
                ) t join media on t.EssayId=media.EssayId
                group by t.EssayId,t.CdnPath,t.MediaExtension
                ) t2 
                join essay on t2.EssayId=essay.Id 
                join kuser on essay.CreatorUserId=kuser.Id  
                join tag on essay.Id=tag.EssayId and tag.Sort=1 
                order by(essay.LikeNum+essay.ShareNum+essay.BrowseNum+essay.CommentNum)  desc,essay.CreationTime desc";
                    var creationTime = DateTime.Now.AddYears(-7);
                    param = new { CreationTime = creationTime, Count = count };
                    break;
                case "妆品":
                case "潮拍":
                case "创意":
                    //case "摘录":
                    sql = @"select essay.Id,essay.Category,essay.ShareNum,essay.LikeNum,essay.BrowseNum,essay.CommentNum,essay.title,essay.Location,essay.CreatorUserId,essay.CreationTime,kuser.AvatarUrl,kuser.NickName CreatorNickName,t2.MediaCount,t2.CdnPath,t2.MediaExtension,tag.*  from 
                (
                select t.EssayId,t.CdnPath,t.MediaExtension,count(media.Id) MediaCount
                from (
                select EssayId,CdnPath,MediaExtension from media  join essay on media.EssayId=essay.Id 
                where   media.Sort=1 and media.MediaType='picture' and essay.Category=@Category order by essay.CreationTime desc limit @Count
                ) t join media on t.EssayId=media.EssayId
                group by t.EssayId,t.CdnPath,t.MediaExtension
                ) t2 
                join essay on t2.EssayId=essay.Id 
                join kuser on essay.CreatorUserId=kuser.Id  
                join tag on essay.Id=tag.EssayId and tag.Sort=1 
                order by (essay.LikeNum+essay.ShareNum+essay.BrowseNum+essay.CommentNum) desc,essay.CreationTime desc";

                    param = new { Count = count, Category = type };
                    break;
            }

            //return ConnExecute(conn =>
            //{
            //    var dtoList = new List<TopMediaDto>();
            //    conn.Query<TopMediaDto, TagEntity, bool>(sql, (dto, tag) =>
            //       {
            //           var currentDto = dtoList.FirstOrDefault(d => d.Id == dto.Id);
            //           if (currentDto == null)
            //           {
            //               dto.TagList = new List<TagEntity>();
            //               dto.TagList.Add(tag);
            //               dtoList.Add(dto);
            //           }
            //           else
            //           {
            //               currentDto.TagList.Add(tag);
            //           }

            //           return true;
            //       },
            //      param: param,
            //      splitOn: "Id");

            //    return dtoList;
            //});

            return ConnExecute(conn =>
            {

                var dtoList = conn.Query<TopMediaDto, TagEntity, TopMediaDto>(sql, (dto, tag) =>
                  {
                      dto.TagList = new List<TagEntity>();
                      dto.TagList.Add(tag);
                      return dto;
                  },
                  param: param,
                  splitOn: "Id");

                return dtoList;
            });


        }


        public IEnumerable<TopMediaDto> GetUserMediaPictureList(long userId,int count)
        {
            return ConnExecute(conn =>
            {
                string sql = @"select t.EssayMediaCount,essay.LikeNum EssayLikeNum,media.EssayId,media.CdnPath,media.MediaExtension,essay.Content EssayContent,essay.CreatorUserId,kuser.NickName CreatorNickName from (
                    select  media.EssayId,min(media.Sort) MinSort,count(media.Id) EssayMediaCount
                    from media join essay on media.EssayId=essay.Id and media.MediaType='picture'  
                    where media.CreatorUserId=@CreatorUserId 
                    group by media.EssayId  order by essay.LikeNum desc  limit @Count 
                    ) t join media on t.EssayId=media.EssayId and t.MinSort=media.Sort 
                   join essay on media.EssayId=essay.Id 
                   join kuser on essay.CreatorUserId=kuser.Id   
                  order by EssayLikeNum desc,essay.CreationTime desc";
                var topMediaDtoList = conn.Query<TopMediaDto>(sql, new { CreatorUserId = userId, Count = count });

                return topMediaDtoList;
            });
        }



        public bool IsExistUser(string name, string phone, string email)
        {
            string sql = "select count(1)  from kuser where `Name`=@Name or Phone=@Phone or Email=@Email";
            var result = ConnExecute(conn => conn.ExecuteScalar<int>(sql, new { Name = name, Phone = phone, Email = email }));
            return result > 0;
        }

        public IEnumerable<EssayEntity> GetEssayList(DateTime creationTime)
        {
            return ConnExecute(conn =>
            {

                string sql = @"select *,(select NickName from kuser where Id=essay.CreatorUserId) CreatorUserName 
                from essay 
                left join media on essay.Id=media.EssayId 
                left join tag on essay.Id=tag.EssayId 
                order by essay.LikeNum desc,media.Sort ";
                var essayList = conn.Query<EssayEntity, MediaEntity, TagEntity, EssayEntity>(sql, (essay, media, tag) =>
                {
                    essay.MediaList = essay.MediaList ?? new List<MediaEntity>();
                    essay.TagList = essay.TagList ?? new List<TagEntity>();
                    if (!essay.MediaList.Where(m => m.Id == media.Id).Any())
                    {
                        essay.MediaList.Add(media);
                    }

                    if (!essay.TagList.Where(t => t.Id == tag.Id).Any())
                    {
                        essay.TagList.Add(tag);
                    }

                    return essay;
                },
                  splitOn: "Id");



                return essayList;
            });
        }


        public EssayEntity GetEssay(long id)
        {
            string sql = @"select *,(select NickName from kuser where Id=essay.CreatorUserId) CreatorUserName 
                from essay 
                left join kuser on essay.CreatorUserId=kuser.Id 
                left join media on essay.Id=media.EssayId 
                left join tag on essay.Id=tag.EssayId 
                where essay.id=@EssayId 
               order by media.Sort,tag.Sort";

            return ConnExecute(conn =>
            {
                var essayList = new List<EssayEntity>();
                conn.Query<EssayEntity, KuserEntity, MediaEntity, TagEntity, bool>(sql, (essay, kuser, media, tag) =>
                 {
                     var essayEntity = essayList.FirstOrDefault(e => e.Id == essay.Id);
                     if (essayEntity == null)
                     {
                         essay.Kuser = kuser;
                         essay.MediaList = new List<MediaEntity>();
                         essay.TagList = new List<TagEntity>();
                         essayList.Add(essay);
                     }
                     else
                     {
                         essay = essayEntity;
                     }


                     if (!essay.MediaList.Where(m => m.Id == media.Id).Any())
                     {
                         essay.MediaList.Add(media);
                     }

                     if (!essay.TagList.Where(t => t.Id == tag.Id).Any())
                     {
                         essay.TagList.Add(tag);
                     }

                     return true;
                 },
                  new { EssayId = id },
                  splitOn: "Id");



                return essayList.FirstOrDefault();
            });
        }


        public EssayEntity GetEssay2(long id)
        {

            return ConnExecute(conn =>
            {
                var essayEntity = conn.Get<EssayEntity>(id);
                essayEntity.Kuser = conn.Get<KuserEntity>(essayEntity.CreatorUserId.Value);
                essayEntity.MediaList = conn.GetList<MediaEntity>(new { EssayId = essayEntity.Id }).ToList();
                essayEntity.TagList = conn.GetList<TagEntity>(new { EssayId = essayEntity.Id }).ToList();
                return essayEntity;
            });
        }


        public bool AddEssay(EssayEntity essayEntity, IEnumerable<TagEntity> tagList, IEnumerable<MediaEntity> mediaList)
        {

            return base.TransExecute((conn, trans) =>
            {
                var insertAndGetIdResultDto = conn.CreateAndGetId<EssayEntity, long>(essayEntity, trans);
                if (!insertAndGetIdResultDto.Result)
                {
                    return false;
                }

                tagList = tagList.Select(tag =>
                {
                    tag.EssayId = insertAndGetIdResultDto.Data;
                    return tag;
                });

                var insertListResultDto = conn.CreateList(tagList, trans);
                if (!insertListResultDto.Result)
                {
                    return false;
                }

                mediaList = mediaList.Select(meida =>
                {
                    meida.EssayId = insertAndGetIdResultDto.Data;
                    meida.MediaExtension = meida.MediaExtension.Replace(".", "");
                    return meida;
                });

                return conn.CreateList(mediaList, trans).Result;
            });
        }


        //private ReaderWriterLockSlim _essayLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        //public bool UpdateBrowseNum(int id)
        //{
        //多处改动使用事务级别隔离read committed
        //string sql = @"set session transaction isolation level read committed;
        //                           start transaction;
        //                           update essay set  BrowseNum=(BrowseNum+1) where Id=@Id;
        //                           commit;";
        //单个改动使用update的排他（update\delete自动加上）行锁（使用索引）就行
        //string sql = "update essay set  BrowseNum=(BrowseNum+1) where Id=@Id";

        //_essayLock.EnterWriteLock();
        //try
        //{
        //return TransExecute((conn, trans) =>
        // {
        //     var essayEntity = base.FirstOrDefault<EssayEntity, long>(id, conn, trans);
        //     essayEntity.BrowseNum += essayEntity.BrowseNum;
        //     return base.Update(essayEntity, conn, trans);
        // });
        //}
        //finally {
        //    _essayLock.ExitWriteLock();
        //}
        //}


        //~DefaultRepository()
        //{
        //    if (_essayLock != null) _essayLock.Dispose();
        //}


        public bool UpdateBrowseNum(long id)
        {
            //单个改动使用update的排他（update\delete\insert InnoDB会自动给涉及数据集加上）行锁（使用索引）就行
            string sql = "update essay set  BrowseNum=(BrowseNum+1) where Id=@Id";

            var result = ConnExecute(conn => conn.Execute(sql, new { Id = id }));
            return result > 0;
        }

        public bool ChangeEssayLike(long userId,long essayId, bool isLike)
        {
            //多处改动使用事务时则用事务级别隔离read committed
            string sql = string.Empty;

            if (isLike)
            {
                sql = @"set session transaction isolation level read committed;
                                start transaction;
                                insert essay_like(EssayId,CreatorUserId,CreationTime) values(@EssayId,@CreatorUserId,@CreationTime);
                                update essay set  LikeNum=(LikeNum+1) where Id=@EssayId; 
                                commit;";
            }
            else
            {
                sql = @"set session transaction isolation level read committed;
                                start transaction;
                                delete from essay_like where EssayId=@EssayId and CreatorUserId=@CreatorUserId;
                                update essay set  LikeNum=(LikeNum-1) where Id=@EssayId; 
                                commit;";
            }

            var essayLikeEntity = new EssayLikeEntity
            {
                EssayId = essayId,
                CreatorUserId =  userId,
                CreationTime = DateTime.Now
            };
            var result = ConnExecute(conn => conn.Execute(sql, essayLikeEntity));
            return result > 0;
        }

        public ResultDto AddTask(LongTaskEntity entity) {
            var taskDate = entity.StartDate;
            var taskWeekDay = (int)taskDate.DayOfWeek;
            var taskWeekList = entity.Week.Split(',').Select(w => Convert.ToInt32(w));

            return TransExecute((conn, trans) =>
            {
                var createResult = new ResultDto();
                var createLongResult = conn.CreateAndGetId<LongTaskEntity, long>(entity, trans);
                if (!createLongResult.Result)
                {
                    _logger.LogError("添加长期目标失败，已撤销：" + createLongResult.Message);
                    createResult.Result = false;
                    createResult.Message = "添加长期目标失败";
                    return createResult;
                }

                var taskEntityList = new List<TaskEntity>();
                while (taskDate <= entity.EndDate)
                {
                    if (taskWeekList.Contains(taskWeekDay))
                    {
                        var taskEntity = new TaskEntity()
                        {
                            LongTaskId = createLongResult.Data,
                            TaskDate = taskDate,
                            StartTime = entity.StartTime,
                            EndTime = entity.EndTime,
                            Content = entity.Content,
                            IsRemind = entity.IsRemind,
                            IsDone = false
                        };
                        taskEntity.AuditCreation(entity.CreatorUserId.Value);
                        taskEntityList.Add(taskEntity);
                    }

                    taskDate = taskDate.AddDays(1);
                    taskWeekDay = (taskWeekDay + 1) % 7;
                }


                if (!conn.CreateList(taskEntityList, trans).Result)
                {
                    _logger.LogError("添加小目标失败，已撤销");
                    createResult.Result = false;
                    createResult.Message = "添加小目标失败";
                    return createResult;
                }

                createResult.Result = true;
                return createResult;
            });
        }

    }
}
