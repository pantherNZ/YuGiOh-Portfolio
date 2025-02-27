from asyncio.windows_events import NULL
import requests, json, import_to_trello
from urllib.parse import urlencode
from datetime import datetime
from ratelimiter import RateLimiter
from rarities_data import *

BASE_URL = 'https://db.ygoprodeck.com/api/v7/cardinfo.php'
LAST_DATE_FILE = 'LastQuery.txt'
RESULTS_FILE = 'Results_{}.txt'

current_date = datetime.today().strftime('%m/%d/%Y')

@RateLimiter(max_calls=20, period=1)
def request(base_url , *res, **params):
    url = base_url
    for r in res:
        url = '{}/{}'.format(url, r)
    if params:
        url = '{}?{}'.format(url, urlencode(params))
    print('YGO DB request: ' + url)
    result = requests.request('GET', url)

    if result.status_code == 400:
        print('[ERROR] FAILED request to YGO DB')
        return None

    return result

def load_sets():
    result = json.loads(request('https://db.ygoprodeck.com/api/v7/cardsets.php', '').text)
    sets = {x['set_code']: x['tcg_date'] for x in result if x.get('tcg_date') != None}
    return sets
    

def load_cards(sets:dict, existing_cards:list, card_exclusions:set, previously_fetched_cards:set):
    #last_date = datetime(2000, 1, 1)
    results = []
    
    #if os.path.exists(LAST_DATE_FILE):
    #    with open(LAST_DATE_FILE) as date_file:
    #        last_date = datetime.strptime(date_file.readline(), '%m/%d/%Y')

    existing_cards_set = set(existing_cards)
    existing_cards_no_set = [x[:x.rfind(' (')] for x in existing_cards]
    existing_cards_set_no_set = set(existing_cards_no_set)
    previously_fetched_cards_no_set = set([x[:x.rfind(' (')] for x in previously_fetched_cards])
    added = {}

    for rarity in rarities_query:
        #result = request(BASE_URL, '', sort='new', offset=0, num=100, startdate=last_date, enddate=current_date, rarity=rarity)
        result = request(BASE_URL, '', sort='new', rarity=rarity, race='Dragon')
        result2 = request(BASE_URL, '', sort='new', rarity=rarity, fname='Dragon')

        if result == None or result2 == None:
            continue

        data = json.loads(result.text)['data'] + json.loads(result2.text)['data']
        
        for x in data:
            if not( x['race'] == 'Dragon' or str(x['name']).endswith('Dragon') or str(x['name']).find('Dragon ') != -1 ):
                continue
            if 'Monster' not in x['type']:
                continue
            for card in x['card_sets']:
                if card['set_rarity'] != rarity:
                    continue
                set_code = card['set_code'][:card['set_code'].find('-')]
                #found = sets.get(set_code)
                #if found is None:
                #    continue
                #if datetime.strptime(found, '%Y-%m-%d').date() < last_date.date():
                #    continue
#
                new_card_name = '{0} ({1} - {2})'.format(x['name'], set_code, rarity)
                rarity_start = new_card_name.rfind(' - ')
                set_start = new_card_name.rfind(' (')
                card_set = new_card_name[set_start + 2:rarity_start]
                rarity = new_card_name[rarity_start + 3:-1]
                is_rarity_upgrade = False

                if new_card_name in existing_cards_set:
                    continue

                if new_card_name in card_exclusions:
                    continue
                
                name = new_card_name[:set_start]
                if name in added:
                    if rarities_all.index(rarity) <= rarities_all.index(added[name][0]):
                        continue

                if name in existing_cards_set_no_set:
                    existing = existing_cards[existing_cards_no_set.index(name)]
                    existing_rarity = existing[existing.rfind(' - ') + 3:-1]
                    if existing_rarity not in rarities_all:
                        print(f'[ERROR] Existing card with rarity found not in the rarities list: {existing_rarity}')
                    continue

                colour = None
                if x['race'] != 'Dragon':
                    colour = "yellow"
                elif name not in previously_fetched_cards_no_set:
                    colour = "blue"
                else:
                    colour = "purple"
                added[name] = (rarity, new_card_name, colour)

    return ([x[1] for x in added.values()], {x[1]: x[2] for x in added.values() if x[2] != None})


def sort_results(card:str, colours:dict):
    if card in colours:
        return (colours[card], card)
    return ('zzzz', card)


if __name__ == '__main__':
    board_id = import_to_trello.get_board('Yugioh')
    wants_list = import_to_trello.get_list(board_id, 'Wants Generated')
    current_list = import_to_trello.get_list(board_id, 'Current')

    all_sets = load_sets()

    exclusions_id = import_to_trello.get_list(board_id, 'Exclusions')
    card_exclusions = set(import_to_trello.get_cards(exclusions_id))
    previously_fetched_cards = set(import_to_trello.get_cards(wants_list))

    existing_cards = import_to_trello.get_cards(current_list)
    (new_cards, colours) = load_cards(all_sets, existing_cards, card_exclusions, previously_fetched_cards)
    new_cards.sort(key=lambda x: sort_results(x, colours))

    import_to_trello.archive_old_cards(wants_list)

    import_to_trello.generate_trello_cards(wants_list, new_cards, colours)
