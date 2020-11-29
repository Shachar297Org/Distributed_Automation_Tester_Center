import sys
import json
from utils import *


def CollectInstances(jsonFilePath: str):
    """
    Return list of AWS instances
    """
    out = RunCommand('aws ec2 describe-instances')
    out = out.replace('\r\n', '').replace(' ', '')
    instanceObjects = []
    reservations = json.loads(out)['Reservations']
    for reservationObj in reservations:
        instanceObjects.extend(reservationObj['Instances'])
    instances = []
    for instanceObj in instanceObjects:
        instances.append({'instanceId': instanceObj['InstanceId'], 'dnsName': instanceObj['PublicDnsName'],
                          'state': instanceObj['State']['Name']})
    jsonContent = json.dumps(instances)
    WriteContentToFile(jsonFilePath, jsonContent)


if __name__ == '__main__':
    try:
        jsonFilePath = sys.argv[1]
        CollectInstances(jsonFilePath)
    except Exception as ex:
        print(ex)
        exit(1)
    exit(0)
