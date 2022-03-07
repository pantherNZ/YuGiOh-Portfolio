import requests, json
import TrelloKey
from ratelimiter import RateLimiter

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

# archive all cards in the given list by id
def archive_old_cards(list_id:str):
    print(f'Archiving cards from "Current" list with id: {list_id}')
    response = trello_request('POST', f'https://api.trello.com/1/lists/{list_id}/archiveAllCards')
    
    if response.status_code == 200:
        print('SUCCESS Archiving cards')
    else:
        print(f'[ERROR] FAILED Archiving cards: {response.reason}')


def load_cards(allow_repeats:bool):
     # read txt file data
    with open('yugioh.txt', 'r') as file:
        next(file)
        next(file)
        data = file.readlines()

    # parse data and return it
    entries = []
    unique_check = set()
    for card in data:
        card_data = card.split(',')
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

    # sort alphabetically
    entries.sort()
    return entries


# rate limited (100 per 10 sec) add all the cards processed from above
@RateLimiter(max_calls=100, period=10)
def generate_next_trello_card(list_id:str, card_name:str, idx:int, total:int):
    params = {
        'idList': list_id,
        'name': card_name,
    }    
    response = trello_request('POST', f'https://api.trello.com/1/cards', params=params)
    
    if response.status_code != 200:
        print(f'[ERROR] FAILED adding card with name: {card_name} because: {response.reason}')
        return False

    print(f'Adding card: {card_name} ({idx}/{total})')
    return True


# call func above to add each card name as a trello card
def generate_trello_cards(list_id:str, card_names:list):
    print(f'Adding {len(card_names)} new cards to "Current" list with id: {list_id}')
        
    for card in card_names:
        if not generate_next_trello_card(list_id, card):
            return

    print(f'SUCCESS Finished adding {len(card_names)} new cards')


if __name__ == '__main__':

    board_id = get_board('Yugioh')

    list_id = get_list(board_id, 'Current')

    archive_old_cards(list_id)

    cards = load_cards(allow_repeats=False)

    generate_trello_cards(list_id, cards)