﻿
Imports System.IO
Imports System.Threading
Imports System.Globalization ' For culture info
Imports System.Xml

Public Class frmMain
    Private FirstLoad As Boolean
    Public Const BSDPath As String = "\bsd\"
    Private Const FSDPath As String = "\fsd\"
    Private Const FSDLandMarksPath As String = "\fsd\landmarks\"
    Private Const EVEUniversePath As String = "\fsd\universe\"
    Private Const WormholeUniversePath As String = "\fsd\universe\wormhole\"

    Private Const GridSettingsFileName As String = "GridSettings.txt"
    Public Const ThreadsFileName As String = "NumberofThreads.txt"

    ' For deploying the files to XML for updates
    Private LatestFilesFolder As String
    Private Const MainEXEFile As String = "EVE SDE Database Builder.exe"
    Private Const UpdaterEXEFile As String = "ESDEDB Updater.exe"
    Private Const MySQLDLL As String = "MySql.Data.dll"
    Private Const PostgreSQLDLL As String = "Npgsql.dll"
    Private Const SQLiteBaseDLL As String = "System.Data.SQLite.dll"
    Private Const SQLiteEF6DLL As String = "System.Data.SQLite.EF6.dll"
    Private Const SQLiteLinqDLL As String = "System.Data.SQLite.Linq.dll"
    Private Const YamlDotNetDLL As String = "YamlDotNet.dll"

    Private Const LatestVersionXML As String = "LatestVersionESDEDB.xml"

    ' File URLs
    Private MainEXEFileURL As String = "https://raw.githubusercontent.com/EVEIPH/EVE-SDE-Database-Builder/blob/master/Latest%20Files/EVE%20SDE%20Database%20Builder.exe"
    Private UpdaterEXEFileURL As String = "https://raw.githubusercontent.com/EVEIPH/EVE-SDE-Database-Builder/blob/master/Latest%20Files/EVE%20SDE%20Database%20Builder.exe"
    Private MySQLDLLURL As String = "https://raw.githubusercontent.com/EVEIPH/EVE-SDE-Database-Builder/blob/master/Latest%20Files/MySql.Data.dll"
    Private PostgreSQLDLLURL As String = "https://raw.githubusercontent.com/EVEIPH/EVE-SDE-Database-Builder/blob/master/Latest%20Files/Npgsql.dll"
    Private SQLiteBaseDLLURL As String = "https://raw.githubusercontent.com/EVEIPH/EVE-SDE-Database-Builder/blob/master/Latest%20Files/System.Data.SQLite.dll"
    Private SQLiteEF6DLLURL As String = "https://raw.githubusercontent.com/EVEIPH/EVE-SDE-Database-Builder/blob/master/Latest%20Files/System.Data.SQLite.EF6.dll"
    Private SQLiteLinqDLLURL As String = "https://raw.githubusercontent.com/EVEIPH/EVE-SDE-Database-Builder/blob/master/Latest%20Files/System.Data.SQLite.Linq.dll"
    Private YamlDotNetDLLURL As String = "https://raw.githubusercontent.com/EVEIPH/EVE-SDE-Database-Builder/blob/master/Latest%20Files/YamlDotNet.dll"

    ' For setting the number of threads to use
    Public SelectedThreads As Integer

    Public AllSettings As New ProgramSettings
    Public UserApplicationSettings As ApplicationSettings

    Private CheckedFilesList As List(Of String)

    Private LocalCulture As New CultureInfo("en-US")

    ' Keeps an array of threads if we need to abort update
    Private ThreadsArray As List(Of ThreadList) = New List(Of ThreadList)

    ' For use with updating the grid with files
    Public Structure FileListItem
        Dim FileName As String
        Dim RowLocation As Integer
    End Structure

    ' For use in filling the grid with checks
    Public Structure GridFileItem
        Dim FileName As String
        Dim Checked As Integer
    End Structure

    ' For threading
    Public Structure ThreadList
        Dim T As Thread
        Dim Params As YAMLFilesBase.ImportParameters
    End Structure

#Region "Settings"

    ''' <summary>
    ''' Saves all settings, including the files checked
    ''' </summary>
    Private Sub SaveSettings()

        If Not ConductErrorChecks(False) Then
            Exit Sub
        End If

        With UserApplicationSettings
            .DatabaseName = txtDBName.Text
            .SDEDirectory = lblSDEPath.Text
            .FinalDBPath = lblFinalDBPath.Text

            ' Get the specific settings for each option
            If rbtnAccess.Checked Then
                .SelectedDB = rbtnAccess.Text
                .AccessPassword = txtPassword.Text
            ElseIf rbtnSQLServer.Checked Then
                .SelectedDB = rbtnSQLServer.Text
                .SQLServerName = txtServerName.Text
            ElseIf rbtnMySQL.Checked Then
                .SelectedDB = rbtnMySQL.Text
                .MySQLPassword = txtPassword.Text
                .MySQLServerName = txtServerName.Text
                .MySQLUserName = txtUserName.Text
            ElseIf rbtnPostgreSQL.Checked Then
                .SelectedDB = rbtnPostgreSQL.Text
                .PostgreSQLPassword = txtPassword.Text
                .PostgreSQLServerName = txtServerName.Text
                .PostgreSQLUserName = txtUserName.Text
                .PostgreSQLPort = txtPort.Text
            ElseIf rbtnSQLiteDB.Checked Then
                .SelectedDB = rbtnSQLiteDB.Text
            ElseIf rbtnCSV.Checked Then
                .SelectedDB = rbtnCSV.Text
                .CSVEUCheck = chkEUFormat.Checked
            End If

            ' Language
            If rbtnEnglish.Checked Then
                .SelectedLanguage = rbtnEnglish.Text
            ElseIf rbtnGerman.Checked Then
                .SelectedLanguage = rbtnGerman.Text
            ElseIf rbtnFrench.Checked Then
                .SelectedLanguage = rbtnFrench.Text
            ElseIf rbtnJapanese.Checked Then
                .SelectedLanguage = rbtnJapanese.Text
            ElseIf rbtnRussian.Checked Then
                .SelectedLanguage = rbtnRussian.Text
            ElseIf rbtnChinese.Checked Then
                .SelectedLanguage = rbtnChinese.Text
            End If
        End With

        ' Save the settings
        Call AllSettings.SaveApplicationSettings(UserApplicationSettings)

        ' Save the grid checks now as a stream
        Dim MyStream As StreamWriter
        MyStream = File.CreateText(GridSettingsFileName)

        ' Loop through the grid and save what is checked - nothing fancy just a list of the file names in a text file
        For i = 0 To dgMain.RowCount - 1
            If dgMain.Rows(i).Cells(0).Value <> 0 Then
                MyStream.Write(dgMain.Rows(i).Cells(1).Value & Environment.NewLine)
            End If
        Next

        MyStream.Flush()
        MyStream.Close()

        MsgBox("Settings Saved", vbInformation, Application.ProductName)

    End Sub

    ''' <summary>
    ''' Gets the data for file paths and other settings from a simple text file saved in local directory
    ''' </summary>
    Private Sub GetSettings()
        ' Read the settings file and lines
        Dim BPStream As StreamReader = Nothing
        Dim FieldType As String = ""
        Dim Language As String = ""
        Dim TempLanguage As String = ""

        UserApplicationSettings = AllSettings.LoadApplicationSettings

        With UserApplicationSettings
            txtDBName.Text = .DatabaseName
            lblFinalDBPath.Text = .FinalDBPath
            lblSDEPath.Text = .SDEDirectory

            ' Set the option
            Select Case .SelectedDB
                Case rbtnAccess.Text
                    rbtnAccess.Checked = True
                Case rbtnCSV.Text
                    rbtnCSV.Checked = True
                Case rbtnSQLiteDB.Text
                    rbtnSQLiteDB.Checked = True
                Case rbtnSQLServer.Text
                    rbtnSQLServer.Checked = True
                Case rbtnMySQL.Text
                    rbtnMySQL.Checked = True
                Case rbtnPostgreSQL.Text
                    rbtnPostgreSQL.Checked = True
            End Select

            Select Case .SelectedLanguage
                Case rbtnEnglish.Text
                    rbtnEnglish.Checked = True
                Case rbtnFrench.Text
                    rbtnFrench.Checked = True
                Case rbtnGerman.Text
                    rbtnGerman.Checked = True
                Case rbtnJapanese.Text
                    rbtnJapanese.Checked = True
                Case rbtnRussian.Text
                    rbtnRussian.Checked = True
            End Select
        End With

        ' Now load all the settings based on that option
        Call LoadFormSettings()

    End Sub

    ''' <summary>
    ''' Gets the boxes checked for loading into grid from file
    ''' </summary>
    Private Sub GetGridSettings()
        ' Read the settings file and save all the files checked
        Dim BPStream As StreamReader = Nothing
        CheckedFilesList = New List(Of String)

        Dim Line As String = ""

        If File.Exists(GridSettingsFileName) Then
            BPStream = New StreamReader(GridSettingsFileName)

            Do
                Line = BPStream.ReadLine()
                If Not IsNothing(Line) Then
                    CheckedFilesList.Add(Line)
                End If
            Loop Until Line Is Nothing

            BPStream.Close()
        End If
    End Sub

    ''' <summary>
    ''' Looks up the settings file for threads and returns the number found
    ''' </summary>
    ''' <returns>Number of threads, if not found then returns -1</returns>
    Private Function GetThreadSetting() As Integer
        ' Read the settings file and save all the files checked
        Dim BPStream As StreamReader = Nothing
        CheckedFilesList = New List(Of String)

        Dim Line As String = ""

        If File.Exists(ThreadsFileName) Then
            BPStream = New StreamReader(ThreadsFileName)
            Line = BPStream.ReadLine()
            BPStream.Close()
            If Trim(Line) = "" Then
                Return -1
            Else
                Return CInt(Line)
            End If

        Else
            Return -1
        End If

    End Function

#End Region

    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

        ' Update files first
        Call CheckForUpdates(False)

        FirstLoad = True

        ' Remove the 'Developer' menu if no developer file
        If Not File.Exists("Developer.txt") Then
            MenuStrip1.Items.Remove(DeveloperToolStripMenuItem)
        End If

        ' Get number of threads to use
        SelectedThreads = GetThreadSetting()

        ' Set the latest files folder path, which is one folder up from the root directory
        LatestFilesFolder = IO.Path.GetDirectoryName(Application.StartupPath) & "\Latest Files\"

        ' Add any initialization after the InitializeComponent() call.
        Call GetSettings()
        Call GetGridSettings()

        ' set a tool tip for the EU check box
        ToolTip1.SetToolTip(chkEUFormat, "Replaces commas with semicolons and all decimals with commas in a CSV file")

        ' Sets the CurrentCulture 
        Thread.CurrentThread.CurrentCulture = LocalCulture

        FirstLoad = False

    End Sub

    Private Sub frmMain_Shown(sender As Object, e As EventArgs) Handles Me.Shown
        ' Load the file names in the grid
        Call LoadFileListtoGrid()
        FirstLoad = False
    End Sub

    Private Sub btnBuildDatabase_Click(sender As Object, e As EventArgs) Handles btnBuildDatabase.Click
        Dim FullDBPathName As String = UserApplicationSettings.FinalDBPath & "\" & UserApplicationSettings.DatabaseName
        Dim WasSuccessful As Boolean = False

        If Not ConductErrorChecks(True) Then
            Exit Sub
        End If

        ' Prep form
        CancelImport = False
        btnBuildDatabase.Enabled = False
        btnSaveSettings.Enabled = False
        btnClose.Enabled = False
        btnCancel.Enabled = True
        btnCancel.Focus()

        Dim TimeCheck As Date = Now

        With UserApplicationSettings
            ' Build the db based on selections
            If rbtnSQLiteDB.Checked Then ' SQLite

                Dim NewSQLiteDB As New SQLiteDB(FullDBPathName & ".sqlite", WasSuccessful)

                If WasSuccessful Then
                    Call NewSQLiteDB.BeginSQLiteTransaction()
                    Call BuildEVEDatabase(NewSQLiteDB, DatabaseType.SQLite)
                    Call NewSQLiteDB.CommitSQLiteTransaction()

                    ' Run a vacuum on the new DB to optimize and safe space
                    Call NewSQLiteDB.ExecuteNonQuerySQL("VACUUM")
                    Call NewSQLiteDB.CloseDB()
                Else
                    GoTo ExitProc
                End If

            ElseIf rbtnSQLServer.Checked Then ' Microsoft SQL Server

                Dim NewSQLServerDB As New msSQLDB(.DatabaseName, .SQLServerName, WasSuccessful)
                If WasSuccessful Then
                    Call BuildEVEDatabase(NewSQLServerDB, DatabaseType.SQLServer)
                Else
                    GoTo ExitProc
                End If

            ElseIf rbtnAccess.Checked Then ' Microsoft Access

                Dim NewAccessDB As New msAccessDB(FullDBPathName & ".accdb", .AccessPassword, WasSuccessful)
                If WasSuccessful Then
                    Call BuildEVEDatabase(NewAccessDB, DatabaseType.MSAccess)
                Else
                    GoTo ExitProc
                End If

            ElseIf rbtnCSV.Checked Then ' CSV

                Dim NewCSVDB As New CSVDB(FullDBPathName & "_CSV", WasSuccessful)
                If WasSuccessful Then
                    Call BuildEVEDatabase(NewCSVDB, DatabaseType.CSV)
                Else
                    GoTo ExitProc
                End If

            ElseIf rbtnMySQL.Checked Then ' MySQL

                Dim NewMySQLDB As New MySQLDB(.DatabaseName, .MySQLServerName, .MySQLUserName, .MySQLPassword, WasSuccessful)

                If WasSuccessful Then
                    Call BuildEVEDatabase(NewMySQLDB, DatabaseType.MySQL)
                Else
                    GoTo ExitProc
                End If

            ElseIf rbtnPostgreSQL.Checked Then ' postgreSQL

                Dim NewPostgreSQLDB As New postgreSQLDB(.DatabaseName, .PostgreSQLServerName, .PostgreSQLUserName, .PostgreSQLPassword, .PostgreSQLPort, WasSuccessful)

                If WasSuccessful Then
                    Call BuildEVEDatabase(NewPostgreSQLDB, DatabaseType.PostgreSQL)
                Else
                    GoTo ExitProc
                End If

            End If
        End With
        If CancelImport Then
            CancelImport = False
            Call ResetProgressColumn()
            Call MsgBox("Import Canceled", vbInformation, Application.ProductName)
        Else
            Dim Seconds As Integer = CInt(DateDiff(DateInterval.Second, TimeCheck, Now))
            Call MsgBox("Files Imported in: " & CInt(Seconds \ 60) & " min " & CInt(Seconds Mod 60) & " sec", vbInformation, Application.ProductName)
        End If

ExitProc:
        btnBuildDatabase.Enabled = True
        btnSaveSettings.Enabled = True
        btnClose.Enabled = True
        btnCancel.Enabled = False
        btnBuildDatabase.Focus()

    End Sub

    ''' <summary>
    ''' Builds the EVE Database for the database type sent.
    ''' </summary>
    ''' <param name="UpdateDatabase">Database class to use for building database and import data into.</param>
    ''' <param name="DatabaseType">Type of Database class</param>
    Private Sub BuildEVEDatabase(UpdateDatabase As Object, DatabaseType As DatabaseType)
        Dim ImportFileList As New List(Of FileListItem)
        Dim Parameters As YAMLFilesBase.ImportParameters
        Dim WorkingDirectory As String = ""
        Dim IgnoreTables As New List(Of String)
        Dim ImportTranslationData As Boolean = False
        Dim Translator As YAMLTranslations = Nothing
        Dim TempThreadList As ThreadList = Nothing

        Dim CheckedTranslationTables As New List(Of String)

        ' For later update
        Dim UF As YAMLUniverse = Nothing

        ' Set up the importfile list
        ImportFileList = GetImportFileList(ImportTranslationData)

        ' Reset the third column so it updates properly
        Call ResetProgressColumn()

        lblStatus.Text = "Preparing files..."
        Application.DoEvents()

        ' Depending on the database, we may need to change the CSV directory to process later - also set if we import records in insert statements or bulk here
        If DatabaseType = DatabaseType.MSAccess Or DatabaseType = DatabaseType.PostgreSQL Then
            WorkingDirectory = UserApplicationSettings.SDEDirectory & "\" & "csvtemp"
            UpdateDatabase.SetCSVDirectory(WorkingDirectory)
            Parameters.InsertRecords = False
        ElseIf DatabaseType = DatabaseType.MySQL Then
            WorkingDirectory = UpdateDatabase.GetCSVDirectory
            Parameters.InsertRecords = False
        Else
            WorkingDirectory = UserApplicationSettings.SDEDirectory & "\" & "temp"
            ' Create the working directory
            Call Directory.CreateDirectory(WorkingDirectory)
            Parameters.InsertRecords = True
        End If

        ' Set the language for all imports
        Parameters.ImportLanguageCode = GetImportLanguage()
        Parameters.ReturnList = False

        ' Run translations before anything else if they selected files that require them for lookups or saving
        Translator = New YAMLTranslations(UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, WorkingDirectory & "\tempdb.sqlite")

        ' If we are adding translation files, then import them first so the tables that have data in them already can be queried
        If ImportTranslationData Then

            If CheckedFilesList.Contains(YAMLTranslations.trnTranslationColumnsFile) Then
                Parameters.RowLocation = GetRowLocation(YAMLTranslations.trnTranslationColumnsFile)
                Call Translator.ImportTranslationColumns(Parameters)
            Else
                ' Don't update in the grid
                Call Translator.ImportTranslationColumns(Parameters, False)
            End If

            If CheckedFilesList.Contains(YAMLTranslations.trnTranslationLanguagesFile) Then
                Parameters.RowLocation = GetRowLocation(YAMLTranslations.trnTranslationLanguagesFile)
                Call Translator.ImportTranslationLanguages(Parameters)
            Else
                ' Don't update in the grid
                Call Translator.ImportTranslationLanguages(Parameters, False)
            End If

            If CheckedFilesList.Contains(YAMLTranslations.trnTranslationsFile) Then
                Parameters.RowLocation = GetRowLocation(YAMLTranslations.trnTranslationsFile)
                Call Translator.ImportTranslations(Parameters)
            Else
                ' Don't update in the grid
                Call Translator.ImportTranslations(Parameters, False)
            End If

        End If

        If CancelImport Then
            GoTo CancelImportProcessing
        End If

        lblStatus.Text = "Importing files..."
        Application.DoEvents()

        ' Now open threads for each of the checked files and import them
        For Each YAMLFile In ImportFileList
            With YAMLFile
                ' Set the row location
                Parameters.RowLocation = .RowLocation

                Select Case .FileName
                    Case YAMLagtAgents.agtAgentsFile
                        Dim Agents As New YAMLagtAgents(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf Agents.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        'Call ThreadsArray.Add(ImportFile(New Thread(AddressOf Agents.ImportFile), Parameters))
                    Case YAMLagtAgentTypes.agtAgentTypesFile
                        Dim AgentTypes As New YAMLagtAgentTypes(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf AgentTypes.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf AgentTypes.ImportFile), Parameters))
                    Case YAMLagtResearchAgents.agtResearchAgentsFile
                        Dim ResearchAgents As New YAMLagtResearchAgents(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf ResearchAgents.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf ResearchAgents.ImportFile), Parameters))

                    Case YAMLchrAncestries.chrAncestriesFile
                        Dim CharAncestry As New YAMLchrAncestries(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf CharAncestry.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf CharAncestry.ImportFile), Parameters))
                    Case YAMLchrAttributes.chrAttributesFile
                        Dim CharAttributes As New YAMLchrAttributes(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf CharAttributes.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                       '  Call ThreadsArray.Add(ImportFile(New Thread(AddressOf CharAttributes.ImportFile), Parameters))
                    Case YAMLchrBloodLines.chrBloodlinesFile
                        Dim CharBloodlines As New YAMLchrBloodLines(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf CharBloodlines.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                       '  Call ThreadsArray.Add(ImportFile(New Thread(AddressOf CharBloodlines.ImportFile), Parameters))
                    Case YAMLchrFactions.chrFactionsFile
                        Dim CharFactions As New YAMLchrFactions(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf CharFactions.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                       '  Call ThreadsArray.Add(ImportFile(New Thread(AddressOf CharFactions.ImportFile), Parameters))
                    Case YAMLchrRaces.chrRacesFile
                        Dim CharRaces As New YAMLchrRaces(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf CharRaces.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf CharRaces.ImportFile), Parameters))

                    Case YAMLcrpActivities.crpActivitiesFile
                        Dim CorpActivites As New YAMLcrpActivities(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf CorpActivites.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                       ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf CorpActivites.ImportFile), Parameters))
                    Case YAMLcrpNPCCorporationDivisions.crpNPCCorporationDivisionsFile
                        Dim CorpCorporationDivisions As New YAMLcrpNPCCorporationDivisions(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf CorpCorporationDivisions.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                       ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf CorpCorporationDivisions.ImportFile), Parameters))
                    Case YAMLcrpNPCCorporationResearchFields.crpNPCCorporationResearchFieldsFile
                        Dim CorpCorporationResearchFields As New YAMLcrpNPCCorporationResearchFields(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf CorpCorporationResearchFields.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        'Call ThreadsArray.Add(ImportFile(New Thread(AddressOf CorpCorporationResearchFields.ImportFile), Parameters))
                    Case YAMLcrpNPCCorporations.crpNPCCorporationsFile
                        Dim CorpCorporations As New YAMLcrpNPCCorporations(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf CorpCorporations.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf CorpCorporations.ImportFile), Parameters))
                    Case YAMLcrpNPCCorporationTrades.crpNPCCorporationTradesFile
                        Dim CorpCorporationTrades As New YAMLcrpNPCCorporationTrades(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf CorpCorporationTrades.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf CorpCorporationTrades.ImportFile), Parameters))
                    Case YAMLcrpNPCDivisions.crpNPCDivisionsFile
                        Dim CorpDivisions As New YAMLcrpNPCDivisions(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf CorpDivisions.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf CorpDivisions.ImportFile), Parameters))

                    Case YAMLdgmAttributeCategories.dgmAttributeCategoriesFile
                        Dim DGMAttributeCategories As New YAMLdgmAttributeCategories(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf DGMAttributeCategories.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf DGMAttributeCategories.ImportFile), Parameters))
                    Case YAMLdgmAttributeTypes.dgmAttributeTypesFile
                        Dim DGMAttributeTypes As New YAMLdgmAttributeTypes(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf DGMAttributeTypes.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf DGMAttributeTypes.ImportFile), Parameters))
                    Case YAMLdgmEffects.dgmEffectsFile
                        Dim DGMEffects As New YAMLdgmEffects(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf DGMEffects.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf DGMEffects.ImportFile), Parameters))
                    Case YAMLdgmExpressions.dgmExpressionsFile
                        Dim DGMExpressions As New YAMLdgmExpressions(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf DGMExpressions.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf DGMExpressions.ImportFile), Parameters))
                    Case YAMLdgmTypeAttributes.dgmTypeAttributesFile
                        Dim DGMTypeAttributes As New YAMLdgmTypeAttributes(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf DGMTypeAttributes.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf DGMTypeAttributes.ImportFile), Parameters))
                    Case YAMLdgmTypeEffects.dgmTypeEffectsFile
                        Dim DGMTypeEffects As New YAMLdgmTypeEffects(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf DGMTypeEffects.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf DGMTypeEffects.ImportFile), Parameters))

                    Case YAMLeveUnits.eveUnitsFile
                        Dim EVEUnits As New YAMLeveUnits(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf EVEUnits.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf EVEUnits.ImportFile), Parameters))

                    Case YAMLinvContrabandTypes.invContrabandTypesFile
                        Dim INVContrabandTypes As New YAMLinvContrabandTypes(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf INVContrabandTypes.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf INVContrabandTypes.ImportFile), Parameters))
                    Case YAMLinvControlTowerResourcePurposes.invControlTowerResourcePurposesFile
                        Dim INVControlTowerResourcePurposes As New YAMLinvControlTowerResourcePurposes(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf INVControlTowerResourcePurposes.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf INVControlTowerResourcePurposes.ImportFile), Parameters))
                    Case YAMLinvControlTowerResources.invControlTowerResourcesFile
                        Dim INVControlTowerResources As New YAMLinvControlTowerResources(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf INVControlTowerResources.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf INVControlTowerResources.ImportFile), Parameters))
                    Case YAMLinvFlags.invFlagsFile
                        Dim INVFlags As New YAMLinvFlags(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf INVFlags.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf INVFlags.ImportFile), Parameters))
                    Case YAMLinvItems.invItemsFile
                        Dim INVItems As New YAMLinvItems(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf INVItems.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf INVItems.ImportFile), Parameters))
                    Case YAMLinvMarketGroups.invMarketGroupsFile
                        Dim INVMarketGroups As New YAMLinvMarketGroups(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf INVMarketGroups.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf INVMarketGroups.ImportFile), Parameters))
                    Case YAMLinvMetaGroups.invMetaGroupsFile
                        Dim INVMetaGroups As New YAMLinvMetaGroups(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf INVMetaGroups.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf INVMetaGroups.ImportFile), Parameters))
                    Case YAMLinvMetaTypes.invMetaTypesFile
                        Dim INVMetaTypes As New YAMLinvMetaTypes(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf INVMetaTypes.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf INVMetaTypes.ImportFile), Parameters))
                    Case YAMLinvNames.invNamesFile
                        Dim INVNames As New YAMLinvNames(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf INVNames.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf INVNames.ImportFile), Parameters))
                    Case YAMLinvPositions.invPositionsFile
                        Dim INVPositions As New YAMLinvPositions(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf INVPositions.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf INVPositions.ImportFile), Parameters))
                    Case YAMLinvTypeMaterials.invTypeMaterialsFile
                        Dim INVTypeMaterials As New YAMLinvTypeMaterials(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf INVTypeMaterials.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf INVTypeMaterials.ImportFile), Parameters))
                    Case YAMLinvTypeReactions.invTypeReactionsFile
                        Dim INVTypeReactions As New YAMLinvTypeReactions(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf INVTypeReactions.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf INVTypeReactions.ImportFile), Parameters))
                    Case YAMLinvUniqueNames.invUniqueNamesFile
                        Dim INVUniqueNames As New YAMLinvUniqueNames(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf INVUniqueNames.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf INVUniqueNames.ImportFile), Parameters))

                    Case YAMLmapUniverse.mapUniverseFile
                        Dim MapUniverse As New YAMLmapUniverse(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf MapUniverse.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf MapUniverse.ImportFile), Parameters))

                    Case YAMLplanetSchematics.planetSchematicsFile
                        Dim PlanetSchematics As New YAMLplanetSchematics(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf PlanetSchematics.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf PlanetSchematics.ImportFile), Parameters))
                    Case YAMLplanetSchematicsPinMap.planetSchematicsPinMapFile
                        Dim PlanetPinMapFile As New YAMLplanetSchematicsPinMap(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf PlanetPinMapFile.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf PlanetPinMapFile.ImportFile), Parameters))
                    Case YAMLplanetSchematicsTypeMap.planetSchematicsTypeMapFile
                        Dim PlanetTypeMapFile As New YAMLplanetSchematicsTypeMap(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf PlanetTypeMapFile.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf PlanetTypeMapFile.ImportFile), Parameters))

                    Case YAMLramActivities.ramActivitiesFile
                        Dim RAMActivities As New YAMLramActivities(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf RAMActivities.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf RAMActivities.ImportFile), Parameters))
                    Case YAMLramAssemblyLineStations.ramAssemblyLineStationsFile
                        Dim RAMAssemblyStations As New YAMLramAssemblyLineStations(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf RAMAssemblyStations.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf RAMAssemblyStations.ImportFile), Parameters))
                    Case YAMLramAssemblyLineTypeDetailPerCategory.ramAssemblyLineTypeDetailPerCategoryFile
                        Dim RAMassemblyLineCategories As New YAMLramAssemblyLineTypeDetailPerCategory(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf RAMassemblyLineCategories.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf RAMassemblyLineCategories.ImportFile), Parameters))
                    Case YAMLramAssemblyLineTypeDetailPerGroup.ramAssemblyLineTypeDetailPerGroupFile
                        Dim RAMassemblyLineGroups As New YAMLramAssemblyLineTypeDetailPerGroup(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf RAMassemblyLineGroups.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf RAMassemblyLineGroups.ImportFile), Parameters))
                    Case YAMLramAssemblyLineTypes.ramAssemblyLineTypesFile
                        Dim RAMAssemblyLineTypes As New YAMLramAssemblyLineTypes(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf RAMAssemblyLineTypes.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf RAMAssemblyLineTypes.ImportFile), Parameters))
                    Case YAMLramInstallationTypeContents.ramInstallationTypeContentsFile
                        Dim RAMInstallationType As New YAMLramInstallationTypeContents(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf RAMInstallationType.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf RAMInstallationType.ImportFile), Parameters))

                    Case YAMLstaOperations.staOperationsFile
                        Dim StaOperations As New YAMLstaOperations(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf StaOperations.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf StaOperations.ImportFile), Parameters))
                    Case YAMLstaOperationServices.staOperationServicesFile
                        Dim StaOperationServices As New YAMLstaOperationServices(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf StaOperationServices.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf StaOperationServices.ImportFile), Parameters))
                    Case YAMLstaServices.staServicesFile
                        Dim StaServies As New YAMLstaServices(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf StaServies.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf StaServies.ImportFile), Parameters))
                    Case YAMLstaStations.staStationsFile
                        Dim StaStations As New YAMLstaStations(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf StaStations.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf StaStations.ImportFile), Parameters))
                    Case YAMLstaStationTypes.staStationTypesFile
                        Dim StaStationTypes As New YAMLstaStationTypes(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf StaStationTypes.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf StaStationTypes.ImportFile), Parameters))

                    Case YAMLwarCombatZones.warCombatZonesFile
                        Dim WarCombatZones As New YAMLwarCombatZones(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf WarCombatZones.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf WarCombatZones.ImportFile), Parameters))
                    Case YAMLwarCombatZoneSystems.warCombatZoneSystemsFile
                        Dim WarCombatSystems As New YAMLwarCombatZoneSystems(.FileName, UserApplicationSettings.SDEDirectory & BSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf WarCombatSystems.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf WarCombatSystems.ImportFile), Parameters))

                    Case YAMLlandmarks.landmarksFile
                        Dim Landmarks As New YAMLlandmarks(.FileName, UserApplicationSettings.SDEDirectory & FSDLandMarksPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf Landmarks.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf Landmarks.ImportFile), Parameters))

                    Case YAMLblueprints.blueprintsFile
                        Dim BPs As New YAMLblueprints(.FileName, UserApplicationSettings.SDEDirectory & FSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf BPs.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf BPs.ImportFile), Parameters))
                    Case YAMLcategoryIDs.categoryIDsFile
                        Dim Categories As New YAMLcategoryIDs(.FileName, UserApplicationSettings.SDEDirectory & FSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf Categories.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf Categories.ImportFile), Parameters))
                    Case YAMLcertificates.certificatesFile
                        Dim Certificates As New YAMLcertificates(.FileName, UserApplicationSettings.SDEDirectory & FSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf Certificates.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf Certificates.ImportFile), Parameters))
                    Case YAMLeveGrpahics.eveGraphicsFile
                        Dim EVEGraphics As New YAMLeveGrpahics(.FileName, UserApplicationSettings.SDEDirectory & FSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf EVEGraphics.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf EVEGraphics.ImportFile), Parameters))
                    Case YAMLeveIcons.eveIconsFile
                        Dim EVEIcons As New YAMLeveIcons(.FileName, UserApplicationSettings.SDEDirectory & FSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf EVEIcons.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf EVEIcons.ImportFile), Parameters))
                    Case YAMLgroupIDs.groupIDsFile
                        Dim GroupIDs As New YAMLgroupIDs(.FileName, UserApplicationSettings.SDEDirectory & FSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf GroupIDs.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf GroupIDs.ImportFile), Parameters))
                    Case YAMLskinLicenses.skinLicensesFile
                        Dim SkinLiscences As New YAMLskinLicenses(.FileName, UserApplicationSettings.SDEDirectory & FSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf SkinLiscences.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf SkinLiscences.ImportFile), Parameters))
                    Case YAMLskinMaterials.skinMaterialsFile
                        Dim SkinMats As New YAMLskinMaterials(.FileName, UserApplicationSettings.SDEDirectory & FSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf SkinMats.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf SkinMats.ImportFile), Parameters))
                    Case YAMLskins.skinsFile
                        Dim Skins As New YAMLskins(.FileName, UserApplicationSettings.SDEDirectory & FSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf Skins.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf Skins.ImportFile), Parameters))
                    Case YAMLtournamentRuleSets.tournamentRuleSetsFile
                        Dim TRS As New YAMLtournamentRuleSets(.FileName, UserApplicationSettings.SDEDirectory & FSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf TRS.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf TRS.ImportFile), Parameters))
                    Case YAMLtypeIDs.typeIDsFile
                        Dim TIDs As New YAMLtypeIDs(.FileName, UserApplicationSettings.SDEDirectory & FSDPath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf TIDs.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf TIDs.ImportFile), Parameters))

                    Case YAMLUniverse.UniverseFiles
                        UF = New YAMLUniverse("", UserApplicationSettings.SDEDirectory & EVEUniversePath, UpdateDatabase, Translator)
                        TempThreadList.T = New Thread(AddressOf UF.ImportFile)
                        TempThreadList.Params = Parameters
                        Call ThreadsArray.Add(TempThreadList)
                        'Call ThreadsArray.Add(ImportFile(New Thread(AddressOf UF.ImportFile), Parameters))

                        ' For translation tables, only import if not done above - the final completion will copy over selections
                    Case YAMLTranslations.trnTranslationColumnsFile
                        ' They checked this so, copy for later import
                        Call CheckedTranslationTables.Add(YAMLTranslations.trnTranslationColumnsTable)
                        If Not ImportTranslationData Then
                            TempThreadList.T = New Thread(AddressOf Translator.ImportTranslationColumns)
                            TempThreadList.Params = Parameters
                            Call ThreadsArray.Add(TempThreadList)
                            ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf Translator.ImportTranslationColumns), Parameters))
                        End If
                    Case YAMLTranslations.trnTranslationLanguagesFile
                        ' They checked this so, copy for later import
                        Call CheckedTranslationTables.Add(YAMLTranslations.trnTranslationLanguagesTable)
                        If Not ImportTranslationData Then
                            TempThreadList.T = New Thread(AddressOf Translator.ImportTranslationLanguages)
                            TempThreadList.Params = Parameters
                            Call ThreadsArray.Add(TempThreadList)
                            ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf Translator.ImportTranslationLanguages), Parameters))
                        End If
                    Case YAMLTranslations.trnTranslationsFile
                        ' They checked this so, copy for later import
                        Call CheckedTranslationTables.Add(YAMLTranslations.trnTranslationsTable)
                        If Not ImportTranslationData Then
                            TempThreadList.T = New Thread(AddressOf Translator.ImportTranslations)
                            TempThreadList.Params = Parameters
                            Call ThreadsArray.Add(TempThreadList)
                            ' Call ThreadsArray.Add(ImportFile(New Thread(AddressOf Translator.ImportTranslations), Parameters))
                        End If

                End Select
            End With
        Next

        ' Run the threads based on the number of threads the user wants
        If SelectedThreads = -1 Then
            ' Max threads, just run them all now
            For i = 0 To ThreadsArray.Count - 1
                Call ImportFile(ThreadsArray(i).T, ThreadsArray(i).Params)
            Next
        Else
            Dim ThreadStarted As Boolean = False
            Dim ActiveThreads As Integer = 0
            ' Run only as many threads as they have chosen until done
            For i = 0 To ThreadsArray.Count - 1
                Do ' keep running this loop until the thread starts
                    ' See how many threads are active
                    ActiveThreads = 0
                    For Each Th In ThreadsArray
                        If Th.T.IsAlive Then
                            ActiveThreads += 1
                        End If
                    Next

                    ' Only run if we haven't reached the max threads they wanted to run yet
                    If ActiveThreads < SelectedThreads Then
                        Call ImportFile(ThreadsArray(i).T, ThreadsArray(i).Params)
                        ThreadStarted = True
                    Else
                        ThreadStarted = False ' Wait till a thread opens up
                    End If
                    Application.DoEvents()
                Loop Until ThreadStarted
            Next

        End If

        ' Now wait until all threads finish
        Do While Not ThreadsComplete(ThreadsArray)
            If CancelImport Then
                GoTo CancelImportProcessing
            End If
            Application.DoEvents()
        Loop

        ' Kill any remaining threads
        Call KillThreads()
        ThreadsArray = Nothing

        Select Case DatabaseType
            Case DatabaseType.MSAccess, DatabaseType.PostgreSQL
                Dim NewCSVDB As CSVDB

                If DatabaseType = DatabaseType.MSAccess Then
                    NewCSVDB = New CSVDB(WorkingDirectory, Nothing)
                Else
                    NewCSVDB = New CSVDB(WorkingDirectory, Nothing, True)
                End If

                Call BuildEVEDatabase(NewCSVDB, DatabaseType.CSV)

            Case DatabaseType.MySQL
                Dim NewCSVDB As New CSVDB(WorkingDirectory, Nothing)
                Call BuildEVEDatabase(NewCSVDB, DatabaseType.CSV)

        End Select

        If Not CancelImport Then
            ' Finalize
            UpdateDatabase.FinalizeDataImport(Translator, CheckedTranslationTables)
            ' Close translator
            Call Translator.Close()

            ' Clean up temp directory
            If Directory.Exists(WorkingDirectory) And DatabaseType <> DatabaseType.MySQL Then ' MySQL has a fixed Upload directory, so don't delete it
                Directory.Delete(WorkingDirectory, True)
            End If

        End If

        ' Finally, if they imported universe files, build the map jump tables
        If Not IsNothing(UF) Then
            Call UF.CreateMapJumpTables()
        End If

        lblStatus.Text = ""
        Exit Sub

CancelImportProcessing:
        Call KillThreads()
        Call ResetProgressColumn()
        lblStatus.Text = ""

    End Sub

    ''' <summary>
    ''' Returns the import language we are using based on the radio buttons selected
    ''' </summary>
    ''' <returns>The LanguageCode of the radio button selected</returns>
    Private Function GetImportLanguage() As LanguageCode
        If rbtnEnglish.Checked Then
            Return LanguageCode.English
        ElseIf rbtnGerman.Checked Then
            Return LanguageCode.German
        ElseIf rbtnFrench.Checked Then
            Return LanguageCode.French
        ElseIf rbtnJapanese.Checked Then
            Return LanguageCode.Japanese
        ElseIf rbtnRussian.Checked Then
            Return LanguageCode.Russian
        ElseIf rbtnChinese.Checked Then
            Return LanguageCode.Chinese
        Else
            Return LanguageCode.English
        End If
    End Function

    ''' <summary>
    ''' A list of hard coded table names that require translation table input
    ''' </summary>
    ''' <returns>List of table names requiring translation table input</returns>
    Private Function GetRequiredTablesForTranslations() As List(Of String)
        Dim TempList As New List(Of String)
        TempList.Add("chrAncestries.yaml")
        TempList.Add("chrBloodlines.yaml")
        TempList.Add("chrFactions.yaml")
        TempList.Add("chrRaces.yaml")
        TempList.Add("crpActivities.yaml")
        TempList.Add("crpNPCCorporations.yaml")
        TempList.Add("crpNPCDivisions.yaml")
        TempList.Add("dgmAttributeTypes.yaml")
        TempList.Add("dgmEffects.yaml")
        TempList.Add("eveUnits.yaml")
        TempList.Add("invCategories.yaml")
        TempList.Add("invMarketGroups.yaml")
        TempList.Add("invMetaGroups.yaml")
        TempList.Add("landmarks.staticdata")
        TempList.Add("planetSchematics.yaml")
        TempList.Add("ramActivities.yaml")
        TempList.Add("staOperations.yaml")
        TempList.Add("staServices.yaml")

        TempList.Add("typeIDs.yaml")
        TempList.Add("groupIDs.yaml")
        TempList.Add("categoryIDs.yaml")

        Return TempList
    End Function

    ''' <summary>
    ''' Conducts error checks and returns boolean to determine if they pass
    ''' </summary>
    ''' <param name="CheckFileSelection">Boolean to check selection of files in the table.</param>
    ''' <returns>Boolean on whether the checks passed</returns>
    Private Function ConductErrorChecks(CheckFileSelection As Boolean) As Boolean

        ' Error / Data checks
        If CheckedFilesList.Count = 0 And CheckFileSelection Then
            Call MsgBox("No files selected for import", vbInformation, Application.ProductName)
            Return False
        End If

        If Trim(txtDBName.Text) = "" Then
            Call MsgBox("You must select a databasename", vbInformation, Application.ProductName)
            txtDBName.Focus()
            Return False
        End If

        If Trim(lblSDEPath.Text) = "" Then
            Call MsgBox("You must select a path for the SDE YAML files.", vbInformation, Application.ProductName)
            btnSelectSDEPath.Focus()
            Return False
        End If

        ' Do error checks based on the selections
        If rbtnAccess.Checked Or rbtnSQLiteDB.Checked Or rbtnCSV.Checked Then
            If Trim(lblFinalDBPath.Text) = "" Then
                Call MsgBox("You must select a final database path.", vbInformation, Application.ProductName)
                Return False
            End If
        End If

        If rbtnSQLServer.Checked Or rbtnMySQL.Checked Or rbtnPostgreSQL.Checked Then
            ' Check server name
            If Trim(txtServerName.Text) = "" Then
                Call MsgBox("You must select a server name", vbInformation, Application.ProductName)
                txtServerName.Focus()
                Return False
            End If
        End If

        If rbtnMySQL.Checked Or rbtnPostgreSQL.Checked Then
            ' Check user name
            If Trim(txtUserName.Text) = "" Then
                Call MsgBox("You must select a user name", vbInformation, Application.ProductName)
                txtUserName.Focus()
                Return False
            End If
        End If

        If rbtnMySQL.Checked Or rbtnPostgreSQL.Checked Then ' Access password can be blank
            ' Check password
            If Trim(txtPassword.Text) = "" Then
                Call MsgBox("You must select a password", vbInformation, Application.ProductName)
                txtPassword.Focus()
                Return False
            End If
        End If

        If rbtnPostgreSQL.Checked Then
            ' Check port
            If Trim(txtPort.Text) = "" Then
                Call MsgBox("You must select a port number", vbInformation, Application.ProductName)
                txtPort.Focus()
                Return False
            End If
        End If

        Return True

    End Function

#Region "UpdaterFunctions"

    ''' <summary>
    ''' Checks for program file updates and prompts user to continue
    ''' </summary>
    Public Sub CheckForUpdates(ByVal ShowUpdateMessage As Boolean)
        Dim Response As DialogResult
        ' Program Updater
        Dim Updater As New ProgramUpdater
        Dim UpdateCode As UpdateCheckResult

        ' 1 = Update Available, 0 No Update Available, -1 an error occured and msg box already shown
        UpdateCode = Updater.IsProgramUpdatable

        Select Case UpdateCode
            Case UpdateCheckResult.UpdateAvailable

                Response = MsgBox("Update Available - Do you want to update now?", MessageBoxButtons.YesNo, Application.ProductName)

                If Response = DialogResult.Yes Then
                    ' Run the updater
                    Call Updater.RunUpdate()
                End If
            Case UpdateCheckResult.UpToDate
                If ShowUpdateMessage Then
                    MsgBox("No updates available.", vbInformation, Application.ProductName)
                End If
            Case UpdateCheckResult.UpdateError
                MsgBox("Unable to run update at this time. Please try again later.", vbInformation, Application.ProductName)
        End Select

        ' Clean up files used to check
        Call Updater.CleanUpFiles()

    End Sub

    Private Sub PrepareFilesForUpdateToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles PrepareFilesForUpdateToolStripMenuItem.Click
        Call CopyFilesBuildXML()
    End Sub

    ' Copies all the files from directories and then builds the xml file and saves it here for upload to github
    Private Sub CopyFilesBuildXML()
        Dim NewFilesAdded As Boolean = False

        On Error Resume Next
        Me.Cursor = Cursors.WaitCursor
        Application.DoEvents()

        If MD5CalcFile(MainEXEFile) <> MD5CalcFile(LatestFilesFolder & MainEXEFile) Then
            File.Copy(MainEXEFile, LatestFilesFolder & MainEXEFile, True)
            NewFilesAdded = True
        End If

        If MD5CalcFile(MySQLDLL) <> MD5CalcFile(LatestFilesFolder & MySQLDLL) Then
            File.Copy(MySQLDLL, LatestFilesFolder & MySQLDLL, True)
            NewFilesAdded = True
        End If

        If MD5CalcFile(PostgreSQLDLL) <> MD5CalcFile(LatestFilesFolder & PostgreSQLDLL) Then
            File.Copy(PostgreSQLDLL, LatestFilesFolder & PostgreSQLDLL, True)
            NewFilesAdded = True
        End If

        If MD5CalcFile(SQLiteBaseDLL) <> MD5CalcFile(LatestFilesFolder & SQLiteBaseDLL) Then
            File.Copy(SQLiteBaseDLL, LatestFilesFolder & SQLiteBaseDLL, True)
            NewFilesAdded = True
        End If

        If MD5CalcFile(SQLiteEF6DLL) <> MD5CalcFile(LatestFilesFolder & SQLiteEF6DLL) Then
            File.Copy(SQLiteEF6DLL, LatestFilesFolder & SQLiteEF6DLL, True)
            NewFilesAdded = True
        End If

        If MD5CalcFile(SQLiteLinqDLL) <> MD5CalcFile(LatestFilesFolder & SQLiteLinqDLL) Then
            File.Copy(SQLiteLinqDLL, LatestFilesFolder & SQLiteLinqDLL, True)
            NewFilesAdded = True
        End If

        If MD5CalcFile(YamlDotNetDLL) <> MD5CalcFile(LatestFilesFolder & YamlDotNetDLL) Then
            File.Copy(YamlDotNetDLL, LatestFilesFolder & YamlDotNetDLL, True)
            NewFilesAdded = True
        End If

        On Error GoTo 0

        ' Output the Latest XML File if we have updates
        If NewFilesAdded Then
            Call WriteLatestXMLFile()
        End If

        Me.Cursor = Cursors.Default
        Application.DoEvents()

        MsgBox("Files Deployed, upload to Github for user download.", vbInformation, "Complete")

    End Sub

    ' Writes the sent settings to the sent file name
    Private Sub WriteLatestXMLFile()
        Dim VersionNumber As String = String.Format("Version {0}", My.Application.Info.Version.ToString)

        ' Create XmlWriterSettings.
        Dim XMLSettings As XmlWriterSettings = New XmlWriterSettings()
        XMLSettings.Indent = True

        ' Delete the current latestversion file to rebuild
        File.Delete(LatestVersionXML)

        ' Loop through the settings sent and output each name and value
        Using writer As XmlWriter = XmlWriter.Create(LatestVersionXML, XMLSettings)
            writer.WriteStartDocument()
            writer.WriteStartElement("EVEIPH") ' Root.
            writer.WriteAttributeString("Version", VersionNumber)
            writer.WriteStartElement("LastUpdated")
            writer.WriteString(CStr(Now))
            writer.WriteEndElement()

            writer.WriteStartElement("result")
            writer.WriteStartElement("rowset")
            writer.WriteAttributeString("name", "filelist")
            writer.WriteAttributeString("key", "version")
            writer.WriteAttributeString("columns", "Name,Version,MD5,URL")

            ' Add each file 
            writer.WriteStartElement("row")
            writer.WriteAttributeString("Name", MainEXEFile)
            writer.WriteAttributeString("Version", VersionNumber)
            writer.WriteAttributeString("MD5", MD5CalcFile(LatestFilesFolder & MainEXEFile))
            writer.WriteAttributeString("URL", MainEXEFileURL)
            writer.WriteEndElement()

            writer.WriteStartElement("row")
            writer.WriteAttributeString("Name", MySQLDLL)
            writer.WriteAttributeString("Version", FileVersionInfo.GetVersionInfo(MySQLDLL).FileVersion)
            writer.WriteAttributeString("MD5", MD5CalcFile(LatestFilesFolder & MySQLDLL))
            writer.WriteAttributeString("URL", MySQLDLLURL)
            writer.WriteEndElement()

            writer.WriteStartElement("row")
            writer.WriteAttributeString("Name", PostgreSQLDLL)
            writer.WriteAttributeString("Version", FileVersionInfo.GetVersionInfo(PostgreSQLDLL).FileVersion)
            writer.WriteAttributeString("MD5", MD5CalcFile(LatestFilesFolder & PostgreSQLDLL))
            writer.WriteAttributeString("URL", PostgreSQLDLLURL)
            writer.WriteEndElement()

            writer.WriteStartElement("row")
            writer.WriteAttributeString("Name", SQLiteBaseDLL)
            writer.WriteAttributeString("Version", FileVersionInfo.GetVersionInfo(SQLiteBaseDLL).FileVersion)
            writer.WriteAttributeString("MD5", MD5CalcFile(LatestFilesFolder & SQLiteBaseDLL))
            writer.WriteAttributeString("URL", SQLiteBaseDLL)
            writer.WriteEndElement()

            writer.WriteStartElement("row")
            writer.WriteAttributeString("Name", SQLiteEF6DLL)
            writer.WriteAttributeString("Version", FileVersionInfo.GetVersionInfo(SQLiteEF6DLL).FileVersion)
            writer.WriteAttributeString("MD5", MD5CalcFile(LatestFilesFolder & SQLiteEF6DLL))
            writer.WriteAttributeString("URL", SQLiteEF6DLLURL)
            writer.WriteEndElement()

            writer.WriteStartElement("row")
            writer.WriteAttributeString("Name", SQLiteLinqDLL)
            writer.WriteAttributeString("Version", FileVersionInfo.GetVersionInfo(SQLiteLinqDLL).FileVersion)
            writer.WriteAttributeString("MD5", MD5CalcFile(LatestFilesFolder & SQLiteLinqDLL))
            writer.WriteAttributeString("URL", SQLiteLinqDLLURL)
            writer.WriteEndElement()

            writer.WriteStartElement("row")
            writer.WriteAttributeString("Name", YamlDotNetDLL)
            writer.WriteAttributeString("Version", FileVersionInfo.GetVersionInfo(YamlDotNetDLL).FileVersion)
            writer.WriteAttributeString("MD5", MD5CalcFile(LatestFilesFolder & YamlDotNetDLL))
            writer.WriteAttributeString("URL", YamlDotNetDLL)
            writer.WriteEndElement()

            ' End document.
            writer.WriteEndDocument()
        End Using

        ' Finally, replace all the update file's crlf with lf so that when it's uploaded to git, it works properly on download
        Dim FileText As String = File.ReadAllText(LatestVersionXML)
        FileText = FileText.Replace(vbCrLf, Chr(10))

        ' Write the file back out with new formatting
        File.WriteAllText(LatestVersionXML, FileText)
        File.WriteAllText(LatestFilesFolder & LatestVersionXML, FileText)

    End Sub

    ' MD5 Hash - specify the path to a file and this routine will calculate your hash
    Public Function MD5CalcFile(ByVal filepath As String) As String

        ' Open file (as read-only) - If it's not there, return ""
        If IO.File.Exists(filepath) Then
            Using reader As New System.IO.FileStream(filepath, IO.FileMode.Open, IO.FileAccess.Read)
                Using md5 As New System.Security.Cryptography.MD5CryptoServiceProvider

                    ' hash contents of this stream
                    Dim hash() As Byte = md5.ComputeHash(reader)

                    ' return formatted hash
                    Return ByteArrayToString(hash)

                End Using
            End Using
        End If

        ' Something went wrong
        Return ""

    End Function

    ' MD5 Hash - utility function to convert a byte array into a hex string
    Private Function ByteArrayToString(ByVal arrInput() As Byte) As String

        Dim sb As New System.Text.StringBuilder(arrInput.Length * 2)

        For i As Integer = 0 To arrInput.Length - 1
            sb.Append(arrInput(i).ToString("X2"))
        Next

        Return sb.ToString().ToLower

    End Function

#End Region

#Region "Grid and file list functions"

    ''' <summary>
    ''' Imports the file names for processing from the grid 
    ''' </summary>
    ''' <returns>List of files to process</returns>
    Private Function GetImportFileList(ByRef ImportTranslationData As Boolean) As List(Of FileListItem)
        Dim TempFileListItem As FileListItem
        Dim TempFileList As New List(Of FileListItem)
        Dim AddUniverseFiles As Boolean = False

        ' Build the list of tables that will require Translation Table data
        Dim FileListRequiringTranslationTables As New List(Of String)
        FileListRequiringTranslationTables = GetRequiredTablesForTranslations()

        If UserApplicationSettings.SDEDirectory <> "" Then
            ' First load all that are checked and sort by size decending - universe is always biggest at top (add at end)
            Dim DI As New DirectoryInfo(UserApplicationSettings.SDEDirectory & BSDPath)
            ' Get a reference to each file in that directory.
            Dim FilesList As FileInfo() = DI.GetFiles()

            For i = 0 To FilesList.Count - 1
                ' If it's a checked file, add it to the list
                If CheckedFilesList.Contains(FilesList(i).Name) Then
                    TempFileListItem.FileName = FilesList(i).Name
                    TempFileListItem.RowLocation = GetRowLocation(FilesList(i).Name)
                    ' Set the translation data flag here as we go through the bsd files
                    If FileListRequiringTranslationTables.Contains(FilesList(i).Name) Then
                        ImportTranslationData = True
                    End If
                    Call TempFileList.Add(TempFileListItem)
                End If
            Next

            ' Now FSD files
            DI = New DirectoryInfo(UserApplicationSettings.SDEDirectory & FSDPath)
            ' Get a reference to each file in that directory.
            FilesList = DI.GetFiles()

            For i = 0 To FilesList.Count - 1
                ' If it's a checked file, add it to the list
                If CheckedFilesList.Contains(FilesList(i).Name) Then
                    TempFileListItem.FileName = FilesList(i).Name
                    TempFileListItem.RowLocation = GetRowLocation(FilesList(i).Name)
                    ' Set the translation data flag here as we go through the fsd files
                    If FileListRequiringTranslationTables.Contains(FilesList(i).Name) Then
                        ImportTranslationData = True
                    End If
                    Call TempFileList.Add(TempFileListItem)
                End If
            Next

            ' Landmarks
            DI = New DirectoryInfo(UserApplicationSettings.SDEDirectory & FSDLandMarksPath)
            ' Get a reference to each file in that directory.
            FilesList = DI.GetFiles()

            For i = 0 To FilesList.Count - 1
                ' If it's a checked file, add it to the list
                If CheckedFilesList.Contains(FilesList(i).Name) Then
                    TempFileListItem.FileName = FilesList(i).Name
                    TempFileListItem.RowLocation = GetRowLocation(FilesList(i).Name)
                    ' Set the translation data flag here as we go through the landmark files
                    If FileListRequiringTranslationTables.Contains(FilesList(i).Name) Then
                        ImportTranslationData = True
                    End If
                    Call TempFileList.Add(TempFileListItem)
                End If
            Next
        End If

        ' If selected, add the universe files to the top, which should be the largest to process
        If AddUniverseFiles Or CheckedFilesList.Contains(YAMLUniverse.UniverseFiles) Then
            TempFileListItem.FileName = YAMLUniverse.UniverseFiles
            TempFileListItem.RowLocation = GetRowLocation(YAMLUniverse.UniverseFiles)
            TempFileList.Insert(0, TempFileListItem)
        End If

        Return TempFileList

    End Function

    ''' <summary>
    ''' Loads the file names into the list from the SDE Directory
    ''' </summary>
    ''' <returns>Returns boolean if was able to load the list grid or not.</returns>
    Private Function LoadFileListtoGrid() As Boolean
        Dim Counter As Long = 1 ' Start at 1 since we are adding universe files manually
        Dim TotalFileList As New List(Of GridFileItem)
        Dim TempFile As New GridFileItem

        dgMain.Rows.Clear()

        If UserApplicationSettings.SDEDirectory <> "" Then
            Try
                Dim BSD_DI As New DirectoryInfo(UserApplicationSettings.SDEDirectory & BSDPath)
                Dim BSD_FilesList As FileInfo() = BSD_DI.GetFiles()

                For Each YAMLBSDFile In BSD_FilesList
                    TempFile.FileName = YAMLBSDFile.Name
                    TempFile.Checked = GetGridCheckValue(YAMLBSDFile.Name)
                    TotalFileList.Add(TempFile)
                Next

                Dim FSD_DI As New DirectoryInfo(UserApplicationSettings.SDEDirectory & FSDPath)
                Dim FSD_FilesList As FileInfo() = FSD_DI.GetFiles()

                For Each YAMLFSDFile In FSD_FilesList
                    TempFile.FileName = YAMLFSDFile.Name
                    TempFile.Checked = GetGridCheckValue(YAMLFSDFile.Name)
                    TotalFileList.Add(TempFile)
                Next

                Dim LM_FSD_DI As New DirectoryInfo(UserApplicationSettings.SDEDirectory & FSDLandMarksPath)
                Dim LM_FSD_FilesList As FileInfo() = LM_FSD_DI.GetFiles()

                For Each YAMLLMFSDFile In LM_FSD_FilesList
                    TempFile.FileName = YAMLLMFSDFile.Name
                    TempFile.Checked = GetGridCheckValue(YAMLLMFSDFile.Name)
                    TotalFileList.Add(TempFile)
                Next
            Catch ex As Exception
                Call ShowErrorMessage(ex)
                Return False
                Exit Function
            End Try

        End If

        If TotalFileList.Count > 0 Then
            ' Sort the file list by name
            TotalFileList.Sort(New GridFileItemComparer)

            ' Set the rows in the grid
            dgMain.RowCount = TotalFileList.Count + 1 ' Add 1 for Universe Data

            ' Add universe data manually
            dgMain.Rows(0).Cells(0).Value = GetGridCheckValue(YAMLUniverse.UniverseFiles)
            dgMain.Rows(0).Cells(1).Value = YAMLUniverse.UniverseFiles

            For Each YAMLFile In TotalFileList
                ' Add the name and a blank cell to the grid - check each one
                dgMain.Rows(Counter).Cells(0).Value = GetGridCheckValue(YAMLFile.FileName)
                dgMain.Rows(Counter).Cells(1).Value = YAMLFile.FileName
                Counter += 1
            Next
        End If
        Application.DoEvents()

        Return True

    End Function

    ''' <summary>
    ''' Returns an integer value to determine if the row is checked in the grid with the file name given
    ''' </summary>
    ''' <param name="FileName">Filename to search the grid for a check</param>
    ''' <returns></returns>
    Private Function GetGridCheckValue(FileName As String) As Integer
        If CheckedFilesList.Contains(FileName) Then
            Return 1
        Else
            Return 0
        End If
    End Function

#End Region

#Region "Thread Functions"

    ''' <summary>
    ''' Function to start a thread passed and return a ref to the thread
    ''' </summary>
    ''' <param name="T">Thread variable</param>
    ''' <param name="Params">Import Paramaters</param>
    ''' <returns>Thread begun</returns>
    Private Function ImportFile(T As Thread, Params As YAMLFilesBase.ImportParameters) As Thread

        T.Start(Params)
        Return T

    End Function

    ''' <summary>
    ''' Checks to see if any threads are still open
    ''' </summary>
    ''' <param name="Threads"></param>
    ''' <returns>Returns true if threads are still open, false otherwise</returns>
    Private Function ThreadsComplete(Threads As List(Of ThreadList)) As Boolean
        Dim AllComplete As Boolean = True

        For Each Th In Threads
            If Th.T.IsAlive Then
                AllComplete = False
                Return AllComplete
            End If
        Next

        Return AllComplete
    End Function

    ''' <summary>
    ''' Aborts all threads in the Thread Array (public varaible)
    ''' </summary>
    Private Sub KillThreads()
        ' Kill all the threads
        On Error Resume Next
        For i = 0 To ThreadsArray.Count - 1
            If ThreadsArray(i).T.IsAlive Then
                ThreadsArray(i).T.Abort()
            End If
        Next
        On Error GoTo 0
    End Sub

#End Region

#Region "Row progress update functions"

    ''' <summary>
    ''' Resets the 3rd column (index 2) in the grid for showing progress bars
    ''' </summary>
    Private Sub ResetProgressColumn()
        ' Reset the grid progress
        Dim PColumn As New ProgressColumn

        ' Add the progress column
        PColumn.Name = "Progress"

        If dgMain.Columns.Count = 3 Then
            dgMain.Columns.Remove("Progress")
        End If

        dgMain.Columns.Add(PColumn)
        dgMain.Columns(2).Width = 255

    End Sub

    ''' <summary>
    ''' Looks up the row that has the file name and returns the row number
    ''' </summary>
    ''' <param name="FileName">Filename to search in the grid</param>
    ''' <returns>Row number</returns>
    Private Function GetRowLocation(FileName As String) As Integer
        For i = 0 To dgMain.RowCount - 1
            If dgMain.Rows(i).Cells(1).Value = FileName Then
                Return i
            End If
        Next

        Return 0
    End Function

    ''' <summary>
    ''' Initializes the grid row sent
    ''' </summary>
    ''' <param name="Postion">Grid row</param>
    Public Sub InitGridRow(ByVal Postion As Integer)
        dgMain.Rows(Postion).Cells(2).Value = 0
        Application.DoEvents()
    End Sub

    ''' <summary>
    ''' Updates the grid row as a percentage for the progress bar
    ''' </summary>
    ''' <param name="Postion">Row number to update</param>
    ''' <param name="Count">Current record count</param>
    ''' <param name="TotalRecords">Total records to process</param>
    Public Sub UpdateGridRowProgress(ByVal Postion As Integer, ByVal Count As Integer, ByVal TotalRecords As Integer)
        dgMain.Rows(Postion).Cells(2).Value = CInt(Math.Floor(Count / TotalRecords * 100))
        Application.DoEvents()
    End Sub

    ''' <summary>
    ''' Finalizes the grid row by setting it to 100
    ''' </summary>
    ''' <param name="Postion">Row number</param>
    Public Sub FinalizeGridRow(ByVal Postion As Integer)
        dgMain.Rows(Postion).Cells(2).Value = 100
        Application.DoEvents()
    End Sub

#End Region

#Region "Update Progress Bar on main form"

    ''' <summary>
    ''' Initializes the progress bar on the main form
    ''' </summary>
    ''' <param name="PGMaxCount">Maximum progress bar count</param>
    ''' <param name="UpdateText">Text to display in status label</param>
    Public Sub InitalizeProgress(ByVal PGMaxCount As Long, ByVal UpdateText As String)
        lblStatus.Text = UpdateText

        pgMain.Value = 0
        pgMain.Maximum = PGMaxCount
        If PGMaxCount <> 0 Then
            pgMain.Visible = True
        End If

        Application.DoEvents()

    End Sub

    ''' <summary>
    ''' Resets the progressbar and status label on main form
    ''' </summary>
    Public Sub ClearProgress()
        pgMain.Visible = False
        lblStatus.Text = ""
        Application.DoEvents()

    End Sub

    ''' <summary>
    ''' Increments the progressbar
    ''' </summary>
    ''' <param name="Count">Current count to update on progress bar.</param>
    ''' <param name="UpdateText">Text to display in the status label</param>
    Public Sub UpdateProgress(ByVal Count As Long, ByVal UpdateText As String)
        Count += 1
        If Count < pgMain.Maximum - 1 And Count <> 0 Then
            pgMain.Value = Count
            pgMain.Value = pgMain.Value - 1
            pgMain.Value = Count
        Else
            pgMain.Value = Count
        End If

        lblStatus.Text = UpdateText
        Application.DoEvents()

    End Sub

#End Region

#Region "Option Checks processing"

    ''' <summary>
    ''' Processes when a check is clicked on or off in the grid
    ''' </summary>
    ''' <param name="sender"></param>
    ''' <param name="e"></param>
    Private Sub dgMain_CellContentClick(sender As Object, e As DataGridViewCellEventArgs) Handles dgMain.CellContentClick
        If Not FirstLoad Then
            If e.ColumnIndex = 0 Then
                dgMain.EndEdit() ' make sure it sets the check value correctly
                If Convert.ToBoolean(dgMain.CurrentCell.Value) = True Then
                    ' Checked it - add to the list
                    CheckedFilesList.Add(dgMain.Rows(e.RowIndex).Cells(1).Value)
                Else
                    ' Unchecked it - remove from the list
                    CheckedFilesList.Remove(dgMain.Rows(e.RowIndex).Cells(1).Value)
                End If
            End If
        End If
    End Sub

    ''' <summary>
    ''' Enables or disables check boxes, text boxes, and labels on the main form depending on options sent.
    ''' </summary>
    ''' <param name="Server">Boolean for enabling/disabling the Server Label and Textbox</param>
    ''' <param name="UserName">Boolean for enabling/disabling the User Name Label and Textbox</param>
    ''' <param name="Password">Boolean for enabling/disabling the Password Label and Textbox</param>
    ''' <param name="EUFormatCheck">Boolean for enabling/disabling the EU Format Checkbox</param>
    ''' <param name="Port">Boolean for enabling/disabling the Port Label and Textbox</param>
    ''' <param name="FinalDBFolder">Boolean for enabling/disabling the Final DB Path Label, Button, and Textbox</param>
    Private Sub SetFormObjects(Server As Boolean, UserName As Boolean, Password As Boolean, EUFormatCheck As Boolean, Port As Boolean, FinalDBFolder As Boolean)
        lblServerName.Enabled = Server
        txtServerName.Enabled = Server
        lblUserName.Enabled = UserName
        txtUserName.Enabled = UserName
        lblPassword.Enabled = Password
        txtPassword.Enabled = Password
        chkEUFormat.Visible = EUFormatCheck
        lblPort.Enabled = Port
        txtPort.Enabled = Port

        lblFinalDBPath.Enabled = FinalDBFolder
        lblFinalDBFolder.Enabled = FinalDBFolder
        btnSelectFinalDBPath.Enabled = FinalDBFolder

        If chkEUFormat.Enabled = True And rbtnCSV.Checked Then
            lblDBName.Text = "Folder Name:"
        Else
            lblDBName.Text = "Database Name:"
        End If

        If Not FirstLoad Then
            Call LoadFormSettings()
        End If

    End Sub

    ''' <summary>
    ''' Loads all the text boxes with settings on the main form depending on what radio button is selected
    ''' </summary>
    Private Sub LoadFormSettings()
        With UserApplicationSettings
            ' Set the variables
            If rbtnAccess.Checked Then
                txtServerName.Text = ""
                txtPassword.Text = .AccessPassword
                txtUserName.Text = ""
                txtPort.Text = ""
            ElseIf rbtnSQLiteDB.Checked Then
                txtServerName.Text = ""
                txtPassword.Text = ""
                txtUserName.Text = ""
                txtPort.Text = ""
            ElseIf rbtnSQLServer.Checked Then
                txtServerName.Text = .SQLServerName
                txtPassword.Text = ""
                txtUserName.Text = ""
                txtPort.Text = ""
            ElseIf rbtnCSV.Checked Then
                chkEUFormat.Checked = .CSVEUCheck
                txtServerName.Text = ""
                txtPassword.Text = ""
                txtUserName.Text = ""
                txtPort.Text = ""
            ElseIf rbtnMySQL.Checked Then
                txtServerName.Text = .MySQLServerName
                txtPassword.Text = .MySQLPassword
                txtUserName.Text = .MySQLUserName
                txtPort.Text = ""
            ElseIf rbtnPostgreSQL.Checked Then
                txtServerName.Text = .PostgreSQLServerName
                txtPassword.Text = .PostgreSQLPassword
                txtUserName.Text = .PostgreSQLUserName
                txtPort.Text = .PostgreSQLPort
            End If
        End With
    End Sub

    Private Sub rbtnCSV_CheckedChanged(sender As Object, e As EventArgs) Handles rbtnCSV.CheckedChanged
        If rbtnCSV.Checked Then
            Call SetFormObjects(False, False, False, True, False, True)
        End If
    End Sub

    Private Sub rbtnMySQL_CheckedChanged(sender As Object, e As EventArgs) Handles rbtnMySQL.CheckedChanged
        If rbtnMySQL.Checked Then
            Call SetFormObjects(True, True, True, False, False, False)
        End If
    End Sub

    Private Sub rbtnAccess_CheckedChanged(sender As Object, e As EventArgs) Handles rbtnAccess.CheckedChanged
        If rbtnAccess.Checked Then
            Call SetFormObjects(False, False, True, False, False, True)
        End If
    End Sub

    Private Sub rbtnSQLiteDB_CheckedChanged(sender As Object, e As EventArgs) Handles rbtnSQLiteDB.CheckedChanged
        If rbtnSQLiteDB.Checked Then
            Call SetFormObjects(False, False, False, False, False, True)
        End If
    End Sub

    Private Sub rbtnSQLServer_CheckedChanged(sender As Object, e As EventArgs) Handles rbtnSQLServer.CheckedChanged
        If rbtnSQLServer.Checked Then
            Call SetFormObjects(True, False, False, False, False, False)
        End If
    End Sub

    Private Sub rbtnPostgreSQL_CheckedChanged(sender As Object, e As EventArgs) Handles rbtnPostgreSQL.CheckedChanged
        If rbtnPostgreSQL.Checked Then
            Call SetFormObjects(True, True, True, False, True, False)
        End If
    End Sub

#End Region

#Region "Click event handlers"

    Private Sub SetThreadsUsedToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles SetThreadsUsedToolStripMenuItem.Click
        Dim f1 As New frmThreadSelect
        f1.Threads = SelectedThreads
        f1.ShowDialog()
    End Sub

    Private Sub btnSelectSDEPath_Click(sender As Object, e As EventArgs) Handles btnSelectSDEPath.Click
        FBDialog.RootFolder = Environment.SpecialFolder.Desktop

        If Directory.Exists(UserApplicationSettings.SDEDirectory) Then
            FBDialog.SelectedPath = UserApplicationSettings.SDEDirectory
        Else
            FBDialog.SelectedPath = Application.StartupPath
        End If

        If FBDialog.ShowDialog() = DialogResult.OK Then
            Try
                lblSDEPath.Text = FBDialog.SelectedPath
                UserApplicationSettings.SDEDirectory = FBDialog.SelectedPath
            Catch ex As Exception
                MsgBox(Err.Description, vbExclamation, Application.ProductName)
            End Try
        End If
    End Sub

    Private Sub btnSelectFinalDBPath_Click(sender As Object, e As EventArgs) Handles btnSelectFinalDBPath.Click
        FBDialog.RootFolder = Environment.SpecialFolder.Desktop

        If Directory.Exists(UserApplicationSettings.FinalDBPath) Then
            FBDialog.SelectedPath = UserApplicationSettings.FinalDBPath
        Else
            FBDialog.SelectedPath = Application.StartupPath
        End If

        If FBDialog.ShowDialog() = DialogResult.OK Then
            Try
                lblFinalDBPath.Text = FBDialog.SelectedPath
                UserApplicationSettings.FinalDBPath = FBDialog.SelectedPath
            Catch ex As Exception
                MsgBox(Err.Description, vbExclamation, Application.ProductName)
            End Try
        End If
    End Sub

    Private Sub btnSaveFilePath_Click(sender As Object, e As EventArgs) Handles btnSaveSettings.Click
        If LoadFileListtoGrid() Then
            Call SaveSettings()
        End If
    End Sub

    Private Sub btnCheckNoGridItems_Click(sender As Object, e As EventArgs) Handles btnCheckNoGridItems.Click
        For i = 0 To dgMain.RowCount - 1
            dgMain.Rows(i).Cells(0).Value = 0
        Next

        ' Reset all checked files
        CheckedFilesList = New List(Of String)
    End Sub

    Private Sub btnCheckAllGridItems_Click(sender As Object, e As EventArgs) Handles btnCheckAllGridItems.Click
        For i = 0 To dgMain.RowCount - 1
            dgMain.Rows(i).Cells(0).Value = 1
            ' Add all rows
            CheckedFilesList.Add(dgMain.Rows(i).Cells(1).Value)
        Next
    End Sub

    Private Sub btnCancel_Click(sender As Object, e As EventArgs) Handles btnCancel.Click
        CancelImport = True
    End Sub

    Private Sub btnClose_Click(sender As Object, e As EventArgs) Handles btnClose.Click
        End
    End Sub

    Private Sub txtDBName_TextChanged(sender As Object, e As EventArgs) Handles txtDBName.TextChanged
        UserApplicationSettings.DatabaseName = txtDBName.Text
    End Sub

    Private Sub txtDBName_GotFocus(sender As Object, e As EventArgs) Handles txtDBName.GotFocus
        txtDBName.SelectAll()
    End Sub

    Private Sub txtServerName_GotFocus(sender As Object, e As EventArgs) Handles txtServerName.GotFocus
        txtServerName.SelectAll()
    End Sub

    Private Sub txtUserName_GotFocus(sender As Object, e As EventArgs) Handles txtUserName.GotFocus
        txtUserName.SelectAll()
    End Sub

    Private Sub txtPassword_GotFocus(sender As Object, e As EventArgs) Handles txtPassword.GotFocus
        txtPassword.SelectAll()
    End Sub

    Private Sub txtPort_GotFocus(sender As Object, e As EventArgs) Handles txtPort.GotFocus
        txtPort.SelectAll()
    End Sub

    Private Sub AboutToolStripMenuItem1_Click(sender As Object, e As EventArgs) Handles AboutToolStripMenuItem1.Click
        Dim f1 = New frmAbout
        f1.ShowDialog()
    End Sub

#End Region

    ' Predicate for sorting a list of grid file items
    Public Class GridFileItemComparer

        Implements IComparer(Of GridFileItem)

        Public Function Compare(ByVal F1 As GridFileItem, ByVal F2 As GridFileItem) As Integer Implements IComparer(Of GridFileItem).Compare
            ' ascending sort
            Return F1.FileName.CompareTo(F2.FileName)
        End Function

    End Class

End Class

' For updating the data grid view
Public Class ProgressColumn
    Inherits DataGridViewColumn

    Public Sub New()
        MyBase.New(New ProgressCell())
    End Sub

    Public Overrides Property CellTemplate() As DataGridViewCell
        Get
            Return MyBase.CellTemplate
        End Get
        Set(ByVal Value As DataGridViewCell)
            ' Ensure that the cell used for the template is a ProgressCell.
            If Value IsNot Nothing And Not TypeOf (Value) Is ProgressCell Then
                Throw New InvalidCastException("Must be a ProgressCell")
            End If
            MyBase.CellTemplate = Value
        End Set
    End Property

End Class

Public Class ProgressCell
        Inherits DataGridViewImageCell
    Protected Overrides Function GetFormattedValue(ByVal value As Object, ByVal rowIndex As Integer, ByRef cellStyle As DataGridViewCellStyle,
                                                   ByVal valueTypeConverter As System.ComponentModel.TypeConverter,
                                                   ByVal formattedValueTypeConverter As System.ComponentModel.TypeConverter,
                                                   ByVal context As DataGridViewDataErrorContexts) As Object
        ' Create bitmap.
        Dim bmp As Bitmap = New Bitmap(Me.Size.Width, Me.Size.Height)

        Using g As Graphics = Graphics.FromImage(bmp)

            If Not IsNothing(Me.Value) Then
                ' Percentage.
                Dim percentage As Double = 0
                Double.TryParse(Me.Value.ToString(), percentage)
                Dim text As String = percentage.ToString() + " %"

                ' Get width and height of text.
                Dim f As Font = New Font("Microsoft Sans Serif", 8.5, FontStyle.Regular)
                Dim w As Integer = CType(g.MeasureString(text, f).Width, Integer)
                Dim h As Integer = CType(g.MeasureString(text, f).Height, Integer)

                ' Draw pile - build a white box first to cover the value in the grid so it doesn't overlap
                g.DrawRectangle(Pens.Black, 1, 1, Me.Size.Width - 6, Me.Size.Height - 6)
                g.FillRectangle(Brushes.White, 2, 2, CInt((Me.Size.Width - 7)), CInt(Me.Size.Height - 7))
                ' Draw the green progress rectangle based on the number
                g.DrawRectangle(Pens.Black, 1, 1, Me.Size.Width - 6, Me.Size.Height - 6)
                g.FillRectangle(Brushes.LimeGreen, 2, 2, CInt((Me.Size.Width - 7) * percentage / 100), CInt(Me.Size.Height - 7))

                Dim rect As RectangleF = New RectangleF(0, 3, bmp.Width, bmp.Height)
                Dim sf As StringFormat = New StringFormat()

                sf.Alignment = StringAlignment.Center
                g.DrawString(text, f, Brushes.Black, rect, sf)
            End If
        End Using

        Return bmp
    End Function

End Class