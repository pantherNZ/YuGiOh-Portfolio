from asyncio.windows_events import NULL
import requests, os, json, import_to_trello
from urllib.parse import urlencode
from datetime import datetime
from ratelimiter import RateLimiter

BASE_URL = 'https://db.ygoprodeck.com/api/v7/cardinfo.php'
LAST_DATE_FILE = 'LastQuery.txt'
RESULTS_FILE = 'Results_{}.txt'

rarities_query = ["Ultra Rare",
"Ultra Parallel Rare",
"Secret Rare",
"Extra Secret Rare",
"Gold Secret Rare",
"Prismatic Secret Rare",
"Platinum Secret Rare",
"Ultimate Rare",
"Gold Rare",
"Premium Gold Rare",
"Collector's Rare",
"Ghost/Gold Rare",
"Starlight Rare",
"Ghost Rare",
"10000 Secret Rare"]

rarities_all = [
"Common",
"Rare",
"Super Rare",
"Ultra Rare",
"Ultra Parallel Rare",
"Duel Terminal Ultra Parallel Rare",
"Starfoil Rare",
"Mosaic Rare",
"Secret Rare",
"Extra Secret Rare",
"Gold Secret Rare",
"Prismatic Secret Rare",
"Platinum Secret Rare",
"Ultimate Rare",
"Gold Rare",
"Premium Gold Rare",
"Collectors Rare",
"Collector's Rare",
"Ghost/Gold Rare",
"Starlight Rare",
"Ghost Rare",
"10000 Secret Rare"]

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
    

def load_cards(sets:dict, existing_cards:list):
    #last_date = datetime(2000, 1, 1)
    results = []
    
    #if os.path.exists(LAST_DATE_FILE):
    #    with open(LAST_DATE_FILE) as date_file:
    #        last_date = datetime.strptime(date_file.readline(), '%m/%d/%Y')

    existing_cards_set = set(existing_cards)
    existing_cards_no_set = [x[:x.rfind(' (')] for x in existing_cards]
    existing_cards_set_no_set = set(existing_cards_no_set)
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
                
                name = new_card_name[:set_start]
                if name in added:
                    if rarities_all.index(rarity) <= rarities_all.index(added[name][0]):
                        continue

                if name in existing_cards_set_no_set:
                    existing = existing_cards[existing_cards_no_set.index(name)]
                    existing_rarity = existing[existing.rfind(' - ') + 3:-1]
                    if all_sets.get(card_set):
                        if existing_rarity not in rarities_all:
                            print(f'[ERROR] Existing card with rarity found not in the rarities list: {existing_rarity}')
                        elif rarities_all.index(rarity) <= max(8, rarities_all.index(existing_rarity)) and existing_rarity != 'Super Rare':
                            continue
                        else:
                            is_rarity_upgrade = True

                colour = None
                if x['race'] != 'Dragon':
                    colour = "yellow"
                elif not is_rarity_upgrade:
                    colour = "purple"
                added[name] = (rarity, new_card_name, colour)

    return ([x[1] for x in added.values()], {x[1]: x[2] for x in added.values() if x[2] != None})


if __name__ == '__main__':
    board_id = import_to_trello.get_board('Yugioh')
    wants_list = import_to_trello.get_list(board_id, 'Wants Generated')
    current_list = import_to_trello.get_list(board_id, 'Current')

    all_sets = load_sets()

    existing_cards = import_to_trello.get_cards(current_list)
    (new_cards, colours) = load_cards(all_sets, existing_cards)
    new_cards.sort()

    import_to_trello.archive_old_cards(wants_list)

    import_to_trello.generate_trello_cards(wants_list, new_cards, colours)

    #with open(LAST_DATE_FILE, 'w') as date_file:
    #    date_file.write(current_date)

    #with open(RESULTS_FILE.format(current_date.replace('/','-')), 'w') as result_file:
    #    result_file.writelines(new_cards)