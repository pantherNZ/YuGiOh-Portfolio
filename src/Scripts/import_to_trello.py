import requests, json
import TrelloKey
from ratelimiter import RateLimiter
from pathlib import Path
from rarities_data import rarities_short_names_map


colour_data_file = 'labels_data.json'
colour_data_filename = Path(__file__).with_name(colour_data_file)
with open(colour_data_filename, 'r') as file:
    data = json.load(file)
    colour_map = {x['color']: x['id'] for x in data}

@RateLimiter(max_calls=100, period=10)
def trello_request(request_type:str, command:str, headers=dict(), params=dict()):
    return requests.request(request_type,f'{command}?key={TrelloKey.key}&token={TrelloKey.token}', headers=headers, params=params)


# find a trello board by name
def get_board(board_name:str):
    print(f'Finding board id with name: {board_name}')
    response = trello_request('GET', 'https://api.trello.com/1/members/me/boards')
    data = json.loads(response.text)

    for entry in data:
        if entry['name'] == board_name:
            print(f'SUCCESS found board with id: {entry["id"]}')
            return entry['id']

    print('[ERROR] FAILED finding board')
    return 0


# find a list in the given board (id) by name
def get_list(board_id:str, list_name:str):
    print(f'Finding list id with name: {list_name}')
    response = trello_request('GET', f'https://api.trello.com/1/boards/{board_id}/lists')
    data = json.loads(response.text)
    
    for entry in data:
        if entry['name'] == list_name:
            print(f'SUCCESS found list with id: {entry["id"]}')
            return entry['id']

    print('[ERROR] Failed finding list')
    return 0


# get all cards in the given list by id
def get_cards(list_id:str):
    print(f'Getting cards from list with id: {list_id}')
    response = trello_request('GET', f'https://api.trello.com/1/lists/{list_id}/cards')
    data = json.loads(response.text)
    return [x['name'] for x in data]


# archive all cards in the given list by id
def archive_old_cards(list_id:str):
    print(f'Archiving cards from list with id: {list_id}')
    response = trello_request('POST', f'https://api.trello.com/1/lists/{list_id}/archiveAllCards')
    
    if response.status_code == 200:
        print('SUCCESS Archiving cards')
    else:
        print(f'[ERROR] FAILED Archiving cards: {response.reason}:{response.text}')


# output card labels
def get_board_label_data(board_id:str):
    print(f'Finding label data with board name: {board_id}')
    response = trello_request('GET', f'https://api.trello.com/1/boards/{board_id}/labels')
    data = json.loads(response.text)
    print(f'SUCCESS board label data found and saved to {colour_data_file}')
    with open(colour_data_filename, 'w') as file:
        json.dump(data, file, indent=4)


def load_cards_txt(data: list, allow_repeats:bool):
    # parse data and return it
    entries = []
    unique_check = set()
    for idx, card in enumerate(data):

        if card == '\n' or len(card) <= 10:
            continue

        card_data = card.split(',')

        if len(card_data) < 8:
            print(f'Failed to load info for card on line {idx}: "f{card}"')
            continue

        name = str.join(',', card_data[1:len(card_data)-6]).strip()
        set_name = card_data[len(card_data)-6].strip()
        card_name = f'{name} ({set_name})'
        
        if allow_repeats:
            for _ in range(int(card_data[0])):
                entries.append(card_name)
        elif name not in unique_check:
            unique_check.add(name)
            entries.append(card_name)
        else:
            continue

    return entries


def load_cards_csv(data: list, allow_repeats:bool):
    # parse data and return it
    entries = []
    unique_check = set()
    for idx, card in enumerate(data):

        if card == '\n' or len(card) <= 10:
            continue

        card_data = card.split(',')

        if len(card_data) < 8:
            print(f'Failed to load info for card on line {idx}: "{card}"')
            continue

        num_columns = len(card_data)
        name = str.join(',', card_data[3:num_columns-12]).strip().replace('"', '')
        set_name = card_data[num_columns-12].strip()
        if set_name.find('-') != -1:
            set_name = set_name[set_name.find('-')]

        rarity = card_data[num_columns-9]
        if rarity not in rarities_short_names_map:
            print(f'Failed to find long name for card rarity on line {idx}: "{card}", rarity: {rarity}')
        else:
            rarity = rarities_short_names_map[rarity]
     
        card_name = f'{name} ({set_name} - {rarity})'
        
        if allow_repeats:
            for _ in range(int(card_data[1])):
                entries.append(card_name)
        elif name not in unique_check:
            unique_check.add(name)
            entries.append(card_name)
        else:
            continue

    return entries


# rate limited (100 per 10 sec) add all the cards processed from above
def generate_next_trello_card(list_id:str, card_name:str, idx:int, total:int):
    params = {
        'idList': list_id,
        'name': card_name,
    }
    request = f'https://api.trello.com/1/cards'
    response = trello_request('POST', request, params=params)
    
    if response.status_code != 200:
        request += '?' + '&'.join([f'{x}={y}' for (x,y) in params.items()])
        print(f'[ERROR] FAILED adding card with name: {card_name} because: {response.reason}:{response.text}\nRequest: {request}')
        return None

    print(f'Adding card: {card_name} ({idx}/{total})')
    return json.loads(response.text)['id']


def add_label_colour_to_card(card_id:str, colour:str, card_name:str):
    if colour not in colour_map:
        print(f'[ERROR] FAILED adding label colour to card (not a valid colour): {card_id} colour: {colour}')
        return

    params = {
        'value': colour_map[colour]
    } 
    request = f'https://api.trello.com/1/cards/{card_id}/idLabels'
    response = trello_request('POST', request, params=params)
        
    if response.status_code != 200:
        request += '?' + '&'.join([f'{x}={y}' for (x,y) in params.items()])
        print(f'[ERROR] FAILED adding label colour to card (request failed): {card_id} colour: {colour}\nRequest: {request}')
        return
    
    print(f'Setting colour: {card_name} ({colour})')


# call func above to add each card name as a trello card
def generate_trello_cards(list_id:str, card_names:list, colours:dict = {}):
    print(f'Adding {len(card_names)} new cards to "Current" list with id: {list_id}')
        
    for idx, card in enumerate( card_names ):
        new_card_id = generate_next_trello_card(list_id, card, idx, len(card_names))
        if new_card_id == None:
            return

        if card in colours:
            add_label_colour_to_card(new_card_id, colours[card], card)

    print(f'SUCCESS Finished adding {len(card_names)} new cards')


if __name__ == '__main__':

    board_id = get_board('Yugioh')
    list_id = get_list(board_id, 'Current')

    archive_old_cards(list_id)

    FILE = 'all.csv'
    is_csv = FILE.endswith('.csv')
    filename = Path(__file__).with_name(FILE)

    with open(filename, 'r') as file:
        next(file)
        next(file)
        data = file.readlines()

    cards = load_cards_csv(data, allow_repeats=False) if is_csv else load_cards_txt(data, allow_repeats=False)
    cards.sort()

    generate_trello_cards(list_id, cards)