import requests, json
import TrelloKey

def get_board():
    response = requests.request("GET",f'https://api.trello.com/1/members/me/boards?key={TrelloKey.key}&token={TrelloKey.token}')
    data = json(response.text)

    for entry in data:
        if entry['name'] == 'Yugioh':
            return entry['id']
    return ''

def get_list(board_id, list_name):
    response = requests.request("GET",f'https://api.trello.com/1/boards/{board_id}/lists?key={TrelloKey.key}&token={TrelloKey.token}')
    return response.text

def generate_trello_cards():
    board_id = get_board()

    if len(board_id) == 0:
        print('board not found')
        return

    # get list
    generated_list = get_list(board_id, 'Current')

    if len(board_id) == 0:
        print('board not found')
        return

    # archive cards in list
    response = requests.request("POST",f'https://api.trello.com/1/lists/{id}/archiveAllCards?key={TrelloKey.key}&token={TrelloKey.token}')
    print(json(response.text))

     # read txt file data
    with open('yugioh.txt', 'r') as file:
        next(file,2)
        data = file.read()
        print(data)

    # create new cards for each yugioh

if __name__ == '__main__':
    print_generate_trello_cardsboards()