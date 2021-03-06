
DECLARE @Schema NVARCHAR(MAX),   
        @Table NVARCHAR(MAX),  
       @Col NVARCHAR(MAX)
  
DECLARE Table_Cursor CURSOR   
FOR  
   --پيدا كردن تمام فيلدهاي متني تمام جداول ديتابيس جاري  
  
    Select TABLE_SCHEMA,TABLE_NAME,COLUMN_NAME  From INFORMATION_SCHEMA.COLUMNS info
   where DATA_TYPE in('ntext','text','nvarchar','varchar', 'char', 'nchar')  and 
   info.TABLE_NAME not in(select TABLE_NAME from INFORMATION_SCHEMA.VIEWS)  
  
OPEN Table_Cursor FETCH NEXT FROM  Table_Cursor INTO @Schema, @Table, @Col  
WHILE (@@FETCH_STATUS = 0)  
BEGIN  
   EXEC (  
              'update ['+ @Schema +'].[' + @Table + '] set [' + @Col +']='+  
          
         ' REPLACE(	REPLACE(CAST(['+@Col+'] as nvarchar(max)),NCHAR(1740), NCHAR(1610)) 
										,NCHAR(1603),NCHAR(1705))' 


       --   'update ['+ @Schema +'].[' + @Table + '] set [' + @Col +']='+  
       --     -- ' REPLACE( 
					  --' REPLACE( 
							--	REPLACE( 
							--			REPLACE(
							--					REPLACE(CAST(['+@Col+'] as nvarchar(max))
							--							,NCHAR(unicode(''ك'')), NCHAR(unicode(''ک''))) '+
							--			',NCHAR(unicode(''ي'')),NCHAR(unicode(''ی''))) '+
							--   ',NCHAR(unicode(''ﯼ'')),NCHAR(unicode(''ی''))) '+
       --               ',NCHAR(unicode(''ى'')),NCHAR(unicode(''ی''))) ' --+
       --     -- ',NCHAR(unicode(''ة'')),NCHAR(unicode(''ه'')))'
        )  
    
   FETCH NEXT FROM Table_Cursor INTO @Schema, @Table, @Col  
END CLOSE Table_Cursor DEALLOCATE Table_Cursor  

  --update dbo.Supplier set TitleFa=  
          
  --        REPLACE(	REPLACE(CAST([TitleFa] as nvarchar(max)),NCHAR(1740), NCHAR(1610)) 
		--								,NCHAR(1603),NCHAR(1705)) 