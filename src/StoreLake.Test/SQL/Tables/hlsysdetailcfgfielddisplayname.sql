CREATE TABLE [dbo].[hlsysdetailcfgfielddisplayname]
(
    [fieldkind]        TINYINT NOT NULL
  , [displaynameid]    INT     NOT NULL
  , CONSTRAINT [PK_hlsysdetailcfgfielddisplayname] PRIMARY KEY ([fieldkind])
  , CONSTRAINT [CK_hlsysdetailcfgfielddisplayname_kind] CHECK ([fieldkind] = 82 /* Customer */
                                                            OR [fieldkind] = 83 /* Organisation */
                                                            OR [fieldkind] = 84 /* Product */)
)