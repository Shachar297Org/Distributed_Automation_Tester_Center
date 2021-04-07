import os
import pandas
import datetime
import csv
import json
import socket
from configparser import ConfigParser
import subprocess
import logging
from record import *
from logger import *


def ReadLogFile(logFile, columns=None):
    """
    Read log file and return records list when each record is a dictionary
    """
    resultSet = []
    with open(logFile, 'r') as reader:
        lines = reader.readlines()
        columnsLine = lines[0].strip('\n\"')
        if columns is None:
            columns = columnsLine.split(',')
        for line in lines[1:]:
            if len(line) == 0 or line[0] == '\n':
                continue
            line = line.strip('\n\"')
            fields = line.split(',')
            record = {columns[i]: fields[i] for i in range(len(columns))}
            resultSet.append(record)
    return resultSet


def GenerateNowTime():
    """
    Generate and return the current time in the format YYYY-MM-DD hh:mm:ss
    """
    return datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')


def ConvertSQLDatetime(datetimeStr: str):
    """
    Convert SQL datetime to regular datetime
    """
    dt = None
    try:
        dt = datetime.datetime.strptime(datetimeStr, '%Y-%m-%dT%H:%M:%S')
    except ValueError:
        dt = datetime.datetime.strptime(datetimeStr, '%Y-%m-%dT%H:%M')
    return dt.strftime('%Y-%m-%d %H:%M:%S')


def ConvertDatetimeFromAMPMTo24(datetimeStr: str, fromFormat: str):
    if datetimeStr.endswith('AM'):
        dt = datetime.datetime.strptime(datetimeStr, fromFormat)
        return dt.strftime('%Y-%m-%d %H:%M:%S')
    elif datetimeStr.endswith('PM'):
        dt = datetime.datetime.strptime(datetimeStr, fromFormat)
        hour = dt.hour
        if hour != 12:
            hour += 12
        dt = dt.replace(hour=hour)
        return dt.strftime('%Y-%m-%d %H:%M:%S')
    else:
        #dt = datetime.datetime.strptime(datetimeStr, fromFormat)
        #return dt.strftime('%Y-%m-%d %H:%M:%S')
        
        return datetime.datetime.strptime(datetimeStr, '%d.%m.%Y %H:%M:%S').strftime('%Y-%m-%d %H:%M:%S')

def ReadEventEntriesFromExcelFile(excelFilePath: str, sheetName: str):
    """
    Read event entries from excel file and sheet name
    """
    df = pandas.read_excel(excelFilePath, sheet_name=sheetName)
    columnNames = list(df.keys())
    eventRecords = []
    for index in range(len(df)):
        eventRecord = {columnName: str(df[columnName][index])
                       for columnName in columnNames}
        eventRecords.append(eventRecord)
    return eventRecords


def ReadConfigFile(configFilePath: str):
    """
    Read config file and return config object
    """
    config = ConfigParser()
    config.read(configFilePath)
    return config


def ReadRecordsFromCsvFile(csvFile: str):
    """
    Read records from csv file
    """
    records = []
    with open(csvFile, 'r') as reader:
        lines = reader.readlines()
        fields = [field.strip(' \n') for field in lines[0].split(',')]
        for line in lines[1:]:
            values = [value.strip(' \n') for value in line.split(',')]
            record = {field: values[i].strip(' \n')
                      for i, field in enumerate(fields)}
            records.append(record)
    return records


def ReadRecordsFromJsonFile(jsonFile):
    records = []
    with open(jsonFile, 'r') as reader:
        jsonContent = reader.read()
        records = json.loads(jsonContent)
    return records


def WriteToCsvFile(csvFile: str, records: list):
    """
    Write records to csv file
    """
    if len(records) == 0:
        return
    with open(csvFile, 'w', newline='') as file:
        header = list(records[0].keys())
        writer = csv.DictWriter(file, fieldnames=header)
        writer.writeheader()
        for record in records:
            writer.writerow(record)


def RunExecutable(exeFile: str, args: list, shell: bool):
    """
    Run excecutable file with arguments in background
    """
    exeDir = os.path.dirname(os.path.realpath(exeFile))
    try:
        if shell:
            cmd = ' '.join([exeFile] + args)
            # process = subprocess.Popen('start cmd /K {}'.format(
            #     cmd), cwd=exeDir, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, shell=False)
            process = subprocess.Popen('start cmd /K {}'.format(
                cmd), cwd=exeDir, shell=True)
        else:
            process = subprocess.Popen(
                [exeFile] + args, cwd=exeDir, creationflags=subprocess.DETACHED_PROCESS)
        return process
    except Exception as ex:
        return None


def RunCommand(command: str):
    """
    Run command from cmd shell and return its output
    """
    out = subprocess.check_output(command)
    return out.decode('utf-8')


def WriteContentToFile(file, content):
    with open(file, 'w') as writer:
        writer.write(content)


def ReadContentFromFile(file):
    content = ''
    with open(file, 'r') as reader:
        content = reader.read()
    return content


def ConvertEntryToRecord(entry: object):
    """
    Convert entry with all fields from RDS to record with 5 relevant fields
    """
    deviceType = entry['deviceType']
    serialNum = entry['deviceSerialNumber']
    entryKey = entry['entryKey']
    entryValue = entry['entryValue']
    entryTimeStamp = str(entry['entryTimeStamp'])
    return Record(deviceType, serialNum, entryKey, entryValue, entryTimeStamp)


def ConvertEntryToDeviceRecord(entry: object):
    """
    Convert entry to <SN, GN> record
    """
    deviceType = entry['deviceType']
    serialNum = entry['deviceSerialNumber']
    return Record(deviceType, serialNum, '', '', '')


def ConvertEntryToDeviceDict(entry: object):
    """
    Convert entry to <SN, GN> dictionary
    """
    deviceType = entry['deviceType']
    serialNum = entry['deviceSerialNumber']
    return {'deviceType': deviceType, 'deviceSerialNumber': serialNum}


def LoadConfig():
    configParser = ConfigParser()
    configParser.read('config.ini', encoding='utf-8')
    return configParser


def LoadConfigText(configFile: str):
    config = {}
    with open(configFile, 'r') as reader:
        lines = reader.readlines()
        for line in lines:
            if '=' in line:
                line = line.strip(' \n')
                fields = line.split('=')
                key, value = fields[0], fields[1]
                config[key] = value
    return config


def InitLogger(configParser: object):
    logFile = configParser.get('Logger', 'loggerFile')
    logger = Logger('Test-Center', logFile)
    return logger


def GetThisHostIP():
    """
    Get this PC host IP
    """
    hostname = socket.gethostname()
    return socket.gethostbyname(hostname)
