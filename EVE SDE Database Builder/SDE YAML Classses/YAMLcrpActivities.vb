﻿
Imports YamlDotNet.Serialization
Imports System.IO

Public Class YAMLcrpActivities
    Inherits YAMLFilesBase

    Public Const crpActivitiesFile As String = "crpActivities.yaml"

    Public Sub New(ByVal YAMLFileName As String, ByVal YAMLFilePath As String, ByRef DatabaseRef As Object, ByRef TranslationRef As YAMLTranslations)
        MyBase.New(YAMLFileName, YAMLFilePath, DatabaseRef, TranslationRef)
    End Sub

    ''' <summary>
    ''' Imports the yaml file into the database set in the constructor
    ''' </summary>
    ''' <param name="Params">What the row location is and whether to insert the data or not (for bulk import)</param>
    Public Sub ImportFile(ByVal Params As ImportParameters)
        Dim DSB = New DeserializerBuilder()
        DSB.IgnoreUnmatchedProperties()
        DSB = DSB.WithNamingConvention(New NamingConventions.NullNamingConvention)
        Dim DS As New Deserializer
        DS = DSB.Build

        Dim YAMLRecords As New List(Of crpActivity)
        Dim DataFields As List(Of DBField)
        Dim SQL As String = ""
        Dim Count As Long = 0
        Dim TotalRecords As Long = 0

        ' Build table
        Dim Table As New List(Of DBTableField)
        Table.Add(New DBTableField("activityID", FieldType.tinyint_type, 0, False, True))
        Table.Add(New DBTableField("activityName", FieldType.nvarchar_type, 100, True))
        Table.Add(New DBTableField("description", FieldType.nvarchar_type, 1000, True))
        Call UpdateDB.CreateTable(TableName, Table)

        ' See if we only want to build the table and indexes
        If Not Params.InsertRecords Then
            Exit Sub
        End If

        ' Start processing
        Call InitGridRow(Params.RowLocation)

        Try
            ' Parse the input text
            YAMLRecords = DS.Deserialize(Of List(Of crpActivity))(New StringReader(File.ReadAllText(YAMLFile)))
        Catch ex As Exception
            Call ShowErrorMessage(ex)
        End Try

        TotalRecords = YAMLRecords.Count

        ' Process Data
        For Each DataField In YAMLRecords
            DataFields = New List(Of DBField)

            ' Build the insert list
            DataFields.Add(UpdateDB.BuildDatabaseField("activityID", DataField.activityID, FieldType.tinyint_type))
            DataFields.Add(UpdateDB.BuildDatabaseField("activityName", Translator.TranslateData(TableName, "activityName", "activityID", DataField.activityID, Params.ImportLanguageCode, DataField.activityName), FieldType.nvarchar_type))
            DataFields.Add(UpdateDB.BuildDatabaseField("description", DataField.description, FieldType.nvarchar_type))

            Call UpdateDB.InsertRecord(TableName, DataFields)

            ' Update grid progress
            Call UpdateGridRowProgress(Params.RowLocation, Count, TotalRecords)
            Count += 1

        Next

        Call FinalizeGridRow(Params.RowLocation)

    End Sub

End Class

Public Class crpActivity
    Public Property activityID As Object
    Public Property activityName As Object
    Public Property description As Object
End Class