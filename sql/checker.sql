use EST1C
SELECT 
    p.ProgramId,
    p.Model,
    p.WorkcellName,
    p.FilePath,
    p.FileDate,
    p.DateExtracted,
    COUNT(d.DetailId) AS DetailCount
FROM Program p
LEFT JOIN ProgramDetails d ON p.ProgramId = d.ProgramId
WHERE p.ProgramId = '10c3f62c-4649-4ea7-bddf-72990ef80cc0'
GROUP BY 
    p.ProgramId,
    p.Model,
    p.WorkcellName,
    p.FilePath,
    p.FileDate,
    p.DateExtracted
ORDER BY p.FileDate DESC;


SELECT 
    d.DetailId,
    d.ProgramId,
    d.TargetTorque,
    d.TorqueUnit,
    d.MinAngle,
    d.MaxAngle,
    d.AngleUnit,
    d.ScrewCount,
    d.SpeedRPM
FROM ProgramDetails d
INNER JOIN Program p ON d.ProgramId = p.ProgramId
WHERE p.ProgramId = '10c3f62c-4649-4ea7-bddf-72990ef80cc0'
ORDER BY d.DetailId;

select * from ProgramDetails


  SELECT TOP (1000) [ProgramId]
      ,[Model]
      ,[WorkcellName]
      ,[FilePath]
      ,[FileDate]
      ,[DateExtracted]
  FROM [EST1C].[dbo].[Program]

SELECT TOP (1000) [DetailId]
      ,[ProgramId]
      ,[TargetTorque]
      ,[TorqueUnit]
      ,[MinAngle]
      ,[MaxAngle]
      ,[AngleUnit]
      ,[ScrewCount]
      ,[SpeedRPM]
  FROM [EST1C].[dbo].[ProgramDetails]

SELECT
    (SELECT COUNT(*) FROM [dbo].[Program]) AS TotalPrograms,
    (SELECT COUNT(*) FROM [dbo].[ProgramDetails]) AS TotalProgramDetails;