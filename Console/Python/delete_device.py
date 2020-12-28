import sys
from utils import *
import requests


def SendRequestLogin(config: object):
    """
    Sends to api host login request and return access token
    """
    loginHost = config['API_LOGIN_HOST']
    loginData = {
        "email": config['API_USER'],
        "password": config['API_PASS']
    }
    response = requests.post(
        url=loginHost, headers={'Content-Type': 'application/json'}, data=json.dumps(loginData))
    if not response.ok:
        print('Request failed. status code: {}'.format(response.status_code))
        return None
    jsonObj = response.json()
    return jsonObj['accessToken']


def DeleteDevices(accessToken: str, devicesList, config: object):
    for device in devicesList:
        DeleteDevice(accessToken, device['deviceSerialNumber'], device['deviceType'], config)


def DeleteDevice(accessToken: str, deviceSerialNumber: str, deviceType: str, config: object):
    deviceName = '_'.join([deviceSerialNumber, deviceType])
    #accessToken = SendRequestLogin(config)
    if not accessToken:
        print('Error: Login failed.')
        return
    print('Login success. ')

    host = '/'.join([config['API_DELETE_DEVICE'], 'types',
                     deviceType, 'serialNumbers', deviceSerialNumber])
    print('host: {}'.format(host))
    response = requests.delete(url=host, headers={'Content-Type': 'application/json', 'Authorization': 'Bearer {}'.format(
        accessToken)})
    if response.ok:
        print('Device {} was deleted successfully.'.format(deviceName))

    else:
        print('Error: cannot delete device {}. status code: {}. {}'.format(
            deviceName, response.status_code, response.text))
    

def DeleteDeviceData(accessToken: str, deviceSerialNumber: str, deviceType: str, config: object, dataType: str, fromDate: str, toDate: str):
    deviceName = '_'.join([deviceSerialNumber, deviceType])
    #accessToken = SendRequestLogin(config)
    
    if not accessToken:
        print('Error: Login failed.')
        return
    print('Login success. ')

    host = '/'.join([config['API_PROCESSING_URL'], dataType, 'types',
                     deviceType, 'serialNumbers', deviceSerialNumber])
    
    params = ''
    if fromDate and toDate:
        params = '?from={}&to={}'.format(fromDate, toDate)

    host = host + params
    print('host: {}'.format(host))

    response = requests.delete(url=host, headers={'Content-Type': 'application/json', 'Authorization': 'Bearer {}'.format(
        accessToken)})

    if response.ok:
        print('{} from {} were deleted successfully.'.format(dataType, deviceName))
    else:
        print('Error: cannot delete {} from {}. status code: {}. {}'.format(dataType,
            deviceName, response.status_code, response.text))

    

if __name__ == "__main__":
    from activate_env import *
    ActivateEnv()

    import sys
    import requests
    from utils import *

    print('-----delete_device-----')
    print('Arguments: {}'.format(sys.argv))

    try:
        
        if len(sys.argv) < 4:
            print('Enter arguments: deviceSerialNumber, deviceType and configFile to delete the device')
            print('Enter arguments: deviceSerialNumber, deviceType, configFile, dataType(events or commands), fromDate and toDate to delete device events or commands')
            exit(1)

        deviceSerialNumber = sys.argv[1]
        deviceType = sys.argv[2]
        configFile = sys.argv[3]

        if not os.path.exists(configFile):
            print('Config file {} not exist'.format(configFile))
            exit(2)

        config = LoadConfigText(configFile)
        accessToken = SendRequestLogin(config)

        if len(sys.argv) == 7:
            dataType = sys.argv[4]
            fromDate = sys.argv[5]
            toDate = sys.argv[6]
        
            DeleteDeviceData(accessToken, deviceSerialNumber, deviceType, config, dataType, fromDate, toDate)
        else:
            DeleteDevice(accessToken, deviceSerialNumber, deviceType, config)

        print('-----success-----')
    except Exception as ex:
        print('Error: {}'.format(ex))
        print('-----fail-----')
        exit(1)


