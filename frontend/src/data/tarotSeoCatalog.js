const majorCards = [
  {
    id: 1,
    number: 0,
    slug: 'shut',
    name: 'Шут',
    nameEn: 'The Fool',
    aliases: ['Дурак'],
    summary: 'начало пути, доверие к неизвестному и свобода внутреннего выбора',
    upright: 'В прямом положении Шут говорит о новом этапе, спонтанном решении и готовности выйти за пределы привычного сценария.',
    reversed: 'В перевернутом положении карта предупреждает о наивности, поспешности и риске действовать без опоры на реальность.',
    advice: 'Сохраняйте открытость, но проверьте землю под ногами перед первым шагом.',
    uprightKeywords: ['начало', 'свобода', 'потенциал', 'спонтанность', 'доверие'],
    reversedKeywords: ['наивность', 'опрометчивость', 'хаос', 'безответственность'],
  },
  {
    id: 2,
    number: 1,
    slug: 'mag',
    name: 'Маг',
    nameEn: 'The Magician',
    aliases: [],
    summary: 'воля, мастерство и способность превращать намерение в действие',
    upright: 'Маг показывает момент, когда ресурсы уже собраны, а результат зависит от ясности намерения и личной инициативы.',
    reversed: 'В перевернутом положении Маг указывает на манипуляцию, рассеянность или страх использовать собственный потенциал.',
    advice: 'Сформулируйте цель точно и действуйте теми инструментами, которые уже есть в руках.',
    uprightKeywords: ['воля', 'мастерство', 'действие', 'концентрация', 'творчество'],
    reversedKeywords: ['манипуляция', 'обман', 'нерешительность', 'рассеянность'],
  },
  {
    id: 3,
    number: 2,
    slug: 'verhovnaya-zhritsa',
    name: 'Верховная Жрица',
    nameEn: 'The High Priestess',
    aliases: ['Жрица', 'Папесса'],
    summary: 'интуиция, тайное знание и внимательное слушание внутреннего голоса',
    upright: 'Жрица говорит о скрытых процессах, подсознательных сигналах и знании, которое раскрывается в тишине.',
    reversed: 'В перевернутом положении карта показывает закрытость, подавленную интуицию или нежелание видеть скрытую сторону ситуации.',
    advice: 'Не торопите ответ: сначала отделите тихое знание от тревожных догадок.',
    uprightKeywords: ['интуиция', 'тайна', 'мудрость', 'безмолвие', 'подсознание'],
    reversedKeywords: ['закрытость', 'иллюзии', 'подавление', 'недоверие'],
  },
  {
    id: 4,
    number: 3,
    slug: 'imperatritsa',
    name: 'Императрица',
    nameEn: 'The Empress',
    aliases: [],
    summary: 'изобилие, забота, творчество и естественный рост',
    upright: 'Императрица приносит тему плодородия, телесности, красоты и процессов, которым нужно дать созреть.',
    reversed: 'В перевернутом положении она говорит о творческом блоке, зависимости от заботы или истощении ресурса.',
    advice: 'Поддержите то, что растет, но не превращайте заботу в контроль.',
    uprightKeywords: ['изобилие', 'материнство', 'творчество', 'природа', 'забота'],
    reversedKeywords: ['блок', 'зависимость', 'истощение', 'пренебрежение'],
  },
  {
    id: 5,
    number: 4,
    slug: 'imperator',
    name: 'Император',
    nameEn: 'The Emperor',
    aliases: [],
    summary: 'структура, власть, границы и зрелая ответственность',
    upright: 'Император показывает необходимость порядка, ясных правил и решения, за которое можно отвечать.',
    reversed: 'В перевернутом положении карта предупреждает о жестком контроле, тирании или потере внутреннего стержня.',
    advice: 'Укрепите границы и систему, но оставьте место живому движению.',
    uprightKeywords: ['власть', 'структура', 'стабильность', 'дисциплина', 'защита'],
    reversedKeywords: ['тирания', 'ригидность', 'доминирование', 'контроль'],
  },
  {
    id: 6,
    number: 5,
    slug: 'ierofant',
    name: 'Иерофант',
    nameEn: 'The Hierophant',
    aliases: ['Первосвященник', 'Папа'],
    summary: 'традиция, духовное учение, наставничество и правила сообщества',
    upright: 'Иерофант связывает ситуацию с опытом школы, рода, института или проверенной системы знаний.',
    reversed: 'В перевернутом положении карта говорит о догматизме, конфликте с правилами или необходимости найти собственный путь.',
    advice: 'Отделите живую традицию от привычки подчиняться форме без смысла.',
    uprightKeywords: ['традиция', 'учение', 'духовность', 'наставник', 'институт'],
    reversedKeywords: ['догматизм', 'бунт', 'ортодоксия', 'формальность'],
  },
  {
    id: 7,
    number: 6,
    slug: 'vlyublennye',
    name: 'Влюбленные',
    nameEn: 'The Lovers',
    aliases: [],
    summary: 'союз, любовь, ценности и выбор, который нельзя сделать поверхностно',
    upright: 'Влюбленные говорят о согласии сердца и решения, о союзе людей или частей личности.',
    reversed: 'В перевернутом положении карта показывает дисгармонию, сомнение, неверность себе или выбор против ценностей.',
    advice: 'Выбирайте не только желаемое, но и то, с чем готовы жить дальше.',
    uprightKeywords: ['любовь', 'союз', 'выбор', 'гармония', 'ценности'],
    reversedKeywords: ['дисбаланс', 'измена', 'нерешительность', 'разлад'],
  },
  {
    id: 8,
    number: 7,
    slug: 'kolesnitsa',
    name: 'Колесница',
    nameEn: 'The Chariot',
    aliases: [],
    summary: 'воля, движение, победа и управление разнонаправленными силами',
    upright: 'Колесница указывает на рывок вперед, собранность и способность удерживать курс при сопротивлении.',
    reversed: 'В перевернутом положении карта говорит о потере направления, внутреннем конфликте или агрессивном напоре.',
    advice: 'Сначала согласуйте внутренние силы, затем ускоряйтесь.',
    uprightKeywords: ['победа', 'воля', 'движение', 'контроль', 'фокус'],
    reversedKeywords: ['разногласие', 'агрессия', 'застой', 'потеря курса'],
  },
  {
    id: 9,
    number: 8,
    slug: 'sila',
    name: 'Сила',
    nameEn: 'Strength',
    aliases: ['Вожделение'],
    summary: 'внутренняя смелость, терпение и мягкое владение страстями',
    upright: 'Сила показывает не давление, а спокойную власть над импульсом через доверие, выдержку и сострадание.',
    reversed: 'В перевернутом положении карта говорит о сомнениях, подавленной злости или страхе собственной энергии.',
    advice: 'Не ломайте сопротивление: приручите его через терпение и ясность.',
    uprightKeywords: ['сила', 'терпение', 'смелость', 'самоконтроль', 'сострадание'],
    reversedKeywords: ['слабость', 'страх', 'неуверенность', 'подавление'],
  },
  {
    id: 10,
    number: 9,
    slug: 'otshelnik',
    name: 'Отшельник',
    nameEn: 'The Hermit',
    aliases: [],
    summary: 'уединение, поиск истины и свет внутреннего опыта',
    upright: 'Отшельник ведет к паузе, самостоятельному размышлению и знанию, которое нельзя получить в шуме.',
    reversed: 'В перевернутом положении карта показывает изоляцию, упрямство или отказ принимать помощь.',
    advice: 'Останьтесь наедине с вопросом, но не превращайте паузу в бегство от жизни.',
    uprightKeywords: ['уединение', 'мудрость', 'поиск', 'интроспекция', 'учитель'],
    reversedKeywords: ['изоляция', 'одиночество', 'упрямство', 'закрытость'],
  },
  {
    id: 11,
    number: 10,
    slug: 'koleso-fortuny',
    name: 'Колесо Фортуны',
    nameEn: 'Wheel of Fortune',
    aliases: [],
    summary: 'поворот судьбы, циклы, случай и момент перемены',
    upright: 'Колесо Фортуны говорит о смене фазы: события начинают двигаться по логике большего цикла.',
    reversed: 'В перевернутом положении карта показывает сопротивление переменам, ощущение невезения или повтор старого сценария.',
    advice: 'Смотрите не только на событие, но и на цикл, частью которого оно является.',
    uprightKeywords: ['судьба', 'цикл', 'перемена', 'шанс', 'поворот'],
    reversedKeywords: ['застой', 'невезение', 'сопротивление', 'повтор'],
  },
  {
    id: 12,
    number: 11,
    slug: 'spravedlivost',
    name: 'Справедливость',
    nameEn: 'Justice',
    aliases: [],
    summary: 'истина, равновесие, закон причины и следствия',
    upright: 'Справедливость требует честного взгляда на факты, последствия и личную ответственность.',
    reversed: 'В перевернутом положении карта говорит о перекосе, нечестности, самообмане или уходе от ответственности.',
    advice: 'Назовите вещи своими именами и проверьте, где нарушен баланс.',
    uprightKeywords: ['справедливость', 'истина', 'закон', 'ответственность', 'баланс'],
    reversedKeywords: ['несправедливость', 'ложь', 'уклонение', 'перекос'],
  },
  {
    id: 13,
    number: 12,
    slug: 'poveshenny',
    name: 'Повешенный',
    nameEn: 'The Hanged Man',
    aliases: [],
    summary: 'пауза, смена взгляда, добровольная жертва и отпускание контроля',
    upright: 'Повешенный показывает момент, когда движение вперед возможно только после смены перспективы.',
    reversed: 'В перевернутом положении карта говорит о бесплодной жертвенности, стагнации или отказе увидеть иной ракурс.',
    advice: 'Приостановитесь и спросите, какой контроль уже пора отпустить.',
    uprightKeywords: ['пауза', 'жертва', 'смена взгляда', 'сдача', 'созерцание'],
    reversedKeywords: ['стагнация', 'мученичество', 'тупик', 'сопротивление'],
  },
  {
    id: 14,
    number: 13,
    slug: 'smert',
    name: 'Смерть',
    nameEn: 'Death',
    aliases: ['Аркан без имени'],
    summary: 'завершение, трансформация и освобождение места для нового',
    upright: 'Смерть редко говорит о буквальном конце: чаще это глубокая смена формы, которую уже нельзя отложить.',
    reversed: 'В перевернутом положении карта показывает страх перемен, цепляние за прошлое или затянутый переход.',
    advice: 'Завершите то, что исчерпало жизненную силу, чтобы не тратить энергию на удержание оболочки.',
    uprightKeywords: ['окончание', 'трансформация', 'освобождение', 'переход', 'обновление'],
    reversedKeywords: ['сопротивление', 'страх перемен', 'застой', 'цепляние'],
  },
  {
    id: 15,
    number: 14,
    slug: 'umerennost',
    name: 'Умеренность',
    nameEn: 'Temperance',
    aliases: [],
    summary: 'баланс, исцеление, смешение противоположностей и чувство меры',
    upright: 'Умеренность говорит о мягкой настройке системы, где результат рождается из согласования разных частей.',
    reversed: 'В перевернутом положении карта показывает крайности, нетерпение и потерю внутренней пропорции.',
    advice: 'Ищите не максимальное усилие, а правильную дозировку.',
    uprightKeywords: ['баланс', 'исцеление', 'интеграция', 'терпение', 'мера'],
    reversedKeywords: ['крайности', 'нетерпение', 'дисгармония', 'перегиб'],
  },
  {
    id: 16,
    number: 15,
    slug: 'dyavol',
    name: 'Дьявол',
    nameEn: 'The Devil',
    aliases: [],
    summary: 'привязанности, соблазн, зависимость и честная встреча с тенью',
    upright: 'Дьявол показывает цепи, которые удерживают через страх, желание, выгоду или привычку.',
    reversed: 'В перевернутом положении карта часто говорит об осознании зависимости и возможности выйти из старого узла.',
    advice: 'Спросите, что именно дает привязанность, прежде чем пытаться ее разорвать.',
    uprightKeywords: ['зависимость', 'соблазн', 'тень', 'материальность', 'привязанность'],
    reversedKeywords: ['освобождение', 'осознание', 'отрезвление', 'разрыв'],
  },
  {
    id: 17,
    number: 16,
    slug: 'bashnya',
    name: 'Башня',
    nameEn: 'The Tower',
    aliases: [],
    summary: 'внезапный слом ложной конструкции и освобождающий кризис',
    upright: 'Башня говорит о резком разрушении того, что держалось на иллюзии, гордыне или слабом основании.',
    reversed: 'В перевернутом положении карта показывает попытку избежать неизбежного кризиса или долгое внутреннее напряжение.',
    advice: 'Не спасайте фасад, если сама структура уже просит перестройки.',
    uprightKeywords: ['разрушение', 'прозрение', 'кризис', 'освобождение', 'молния'],
    reversedKeywords: ['избегание', 'отсрочка', 'страх', 'напряжение'],
  },
  {
    id: 18,
    number: 17,
    slug: 'zvezda',
    name: 'Звезда',
    nameEn: 'The Star',
    aliases: [],
    summary: 'надежда, вдохновение, исцеление и тихая вера после испытания',
    upright: 'Звезда возвращает ощущение смысла, показывает дальний ориентир и мягкое восстановление.',
    reversed: 'В перевернутом положении карта говорит о потере веры, усталости или неспособности принять поддержку.',
    advice: 'Дайте себе время восстановиться и не требуйте мгновенного результата.',
    uprightKeywords: ['надежда', 'вдохновение', 'вера', 'исцеление', 'ориентир'],
    reversedKeywords: ['уныние', 'разочарование', 'апатия', 'потеря веры'],
  },
  {
    id: 19,
    number: 18,
    slug: 'luna',
    name: 'Луна',
    nameEn: 'The Moon',
    aliases: [],
    summary: 'иллюзии, страхи, сновидческая логика и подсознательные течения',
    upright: 'Луна показывает область неопределенности, где факты смешиваются с тревогой, фантазией и интуицией.',
    reversed: 'В перевернутом положении карта говорит о рассеивании тумана, раскрытии тайны или выходе из самообмана.',
    advice: 'Не принимайте туман за знак остановки: двигайтесь медленно и проверяйте факты.',
    uprightKeywords: ['иллюзия', 'подсознание', 'интуиция', 'страх', 'тайна'],
    reversedKeywords: ['ясность', 'разоблачение', 'облегчение', 'прозрение'],
  },
  {
    id: 20,
    number: 19,
    slug: 'solntse',
    name: 'Солнце',
    nameEn: 'The Sun',
    aliases: [],
    summary: 'радость, ясность, успех и жизненная сила',
    upright: 'Солнце говорит о раскрытии, признании, простоте и теплом контакте с жизнью.',
    reversed: 'В перевернутом положении карта показывает временную задержку радости, усталость или сомнение в очевидном хорошем.',
    advice: 'Ищите простое и ясное решение: оно ближе, чем кажется.',
    uprightKeywords: ['радость', 'успех', 'жизненность', 'ясность', 'тепло'],
    reversedKeywords: ['усталость', 'задержка', 'неуверенность', 'облако'],
  },
  {
    id: 21,
    number: 20,
    slug: 'sud',
    name: 'Суд',
    nameEn: 'Judgement',
    aliases: ['Страшный суд'],
    summary: 'пробуждение, призвание, итог прошлого и возможность обновления',
    upright: 'Суд поднимает тему внутреннего зова, переоценки прожитого и решения выйти на новый уровень честности.',
    reversed: 'В перевернутом положении карта говорит о самокритике, сожалении или нежелании услышать очевидный призыв.',
    advice: 'Посмотрите на прошлое как на материал для пробуждения, а не как на приговор.',
    uprightKeywords: ['пробуждение', 'искупление', 'призвание', 'возрождение', 'итог'],
    reversedKeywords: ['самокритика', 'сомнение', 'сожаление', 'отказ'],
  },
  {
    id: 22,
    number: 21,
    slug: 'mir',
    name: 'Мир',
    nameEn: 'The World',
    aliases: ['Вселенная'],
    summary: 'завершение, целостность, интеграция и переход на новый круг',
    upright: 'Мир показывает завершенный цикл, собранность опыта и момент, когда разные части наконец становятся целым.',
    reversed: 'В перевернутом положении карта говорит о незавершенности, задержке последнего шага или страхе закрыть этап.',
    advice: 'Признайте достигнутое и завершите круг без лишнего возвращения назад.',
    uprightKeywords: ['завершение', 'целостность', 'интеграция', 'успех', 'переход'],
    reversedKeywords: ['незавершенность', 'задержка', 'стагнация', 'последний шаг'],
  },
]

const suits = [
  {
    label: 'Жезлы',
    genitive: 'Жезлов',
    slugPart: 'zhezlov',
    english: 'Wands',
    folder: 'wands',
    firstId: 23,
    element: 'огонь',
    field: 'воля, инициатива, творчество и движение',
    themes: ['действие', 'страсть', 'инициатива', 'энергия'],
    advice: 'Смотрите, куда направлена энергия и не сжигает ли импульс саму цель.',
  },
  {
    label: 'Кубки',
    genitive: 'Кубков',
    slugPart: 'kubkov',
    english: 'Cups',
    folder: 'cups',
    firstId: 37,
    element: 'вода',
    field: 'чувства, отношения, интуиция и эмоциональная память',
    themes: ['чувства', 'любовь', 'связи', 'эмпатия'],
    advice: 'Смотрите не только на событие, но и на эмоциональную правду, которая за ним стоит.',
  },
  {
    label: 'Мечи',
    genitive: 'Мечей',
    slugPart: 'mechey',
    english: 'Swords',
    folder: 'swords',
    firstId: 51,
    element: 'воздух',
    field: 'мысль, слово, конфликт, выбор и ясность',
    themes: ['мысль', 'слово', 'конфликт', 'ясность'],
    advice: 'Отделите факты от интерпретаций и проверьте, где слово стало оружием.',
  },
  {
    label: 'Пентакли',
    genitive: 'Пентаклей',
    slugPart: 'pentakley',
    english: 'Pentacles',
    folder: 'pentacles',
    firstId: 65,
    element: 'земля',
    field: 'тело, деньги, работа, ресурсы и практический результат',
    themes: ['ресурсы', 'работа', 'тело', 'практика'],
    advice: 'Проверьте материальную основу: время, деньги, тело и реальные обязательства.',
  },
]

const ranks = [
  {
    number: 1,
    label: 'Туз',
    slug: 'tuz',
    english: 'Ace',
    summary: 'чистый потенциал и первая искра масти',
    upright: 'Туз открывает новую возможность и показывает, где энергия только входит в форму.',
    reversed: 'Перевернутый Туз говорит о задержке, потерянном шансе или блоке на самом старте.',
    uprightKeywords: ['начало', 'дар', 'возможность', 'вдохновение'],
    reversedKeywords: ['задержка', 'потеря', 'блок'],
  },
  {
    number: 2,
    label: 'Двойка',
    slug: 'dvoika',
    english: 'Two',
    summary: 'выбор, партнерство и поиск равновесия',
    upright: 'Двойка ставит две силы напротив друг друга и просит найти способ согласования.',
    reversed: 'Перевернутая Двойка показывает разлад, сомнение или нарушение баланса.',
    uprightKeywords: ['выбор', 'баланс', 'партнерство'],
    reversedKeywords: ['дисбаланс', 'разлад', 'сомнение'],
  },
  {
    number: 3,
    label: 'Тройка',
    slug: 'troika',
    english: 'Three',
    summary: 'рост, совместное движение и первые видимые результаты',
    upright: 'Тройка говорит о расширении через сотрудничество, обмен и развитие начатого.',
    reversed: 'Перевернутая Тройка указывает на задержку роста, отсутствие поддержки или разрыв общего ритма.',
    uprightKeywords: ['рост', 'сотрудничество', 'развитие'],
    reversedKeywords: ['задержка', 'изоляция', 'разрыв'],
  },
  {
    number: 4,
    label: 'Четверка',
    slug: 'chetverka',
    english: 'Four',
    summary: 'стабильность, опора и границы формы',
    upright: 'Четверка закрепляет достигнутое и показывает основу, на которой можно стоять.',
    reversed: 'Перевернутая Четверка говорит о застое, чрезмерной защите или страхе потерять контроль.',
    uprightKeywords: ['стабильность', 'основа', 'защита'],
    reversedKeywords: ['застой', 'жадность', 'инерция'],
  },
  {
    number: 5,
    label: 'Пятерка',
    slug: 'pyaterka',
    english: 'Five',
    summary: 'кризис, конфликт и испытание устойчивости',
    upright: 'Пятерка показывает напряжение, через которое система ищет новую форму.',
    reversed: 'Перевернутая Пятерка говорит о восстановлении, выходе из конфликта или возможности примирения.',
    uprightKeywords: ['конфликт', 'испытание', 'потеря'],
    reversedKeywords: ['восстановление', 'примирение', 'освобождение'],
  },
  {
    number: 6,
    label: 'Шестерка',
    slug: 'shesterka',
    english: 'Six',
    summary: 'гармония, обмен и возвращение равновесия',
    upright: 'Шестерка приносит период согласования, признания или здорового обмена.',
    reversed: 'Перевернутая Шестерка показывает временный спад, усталость или задержку естественного потока.',
    uprightKeywords: ['гармония', 'успех', 'щедрость'],
    reversedKeywords: ['спад', 'усталость', 'задержка'],
  },
  {
    number: 7,
    label: 'Семерка',
    slug: 'semerka',
    english: 'Seven',
    summary: 'оценка, стратегия и проверка выбранного пути',
    upright: 'Семерка просит посмотреть на ситуацию внимательнее и выбрать позицию осознанно.',
    reversed: 'Перевернутая Семерка говорит о сомнениях, иллюзии контроля или бездействии.',
    uprightKeywords: ['оценка', 'размышление', 'стратегия'],
    reversedKeywords: ['сомнение', 'бездействие', 'иллюзия'],
  },
  {
    number: 8,
    label: 'Восьмерка',
    slug: 'vosmerka',
    english: 'Eight',
    summary: 'динамика, работа процесса и движение к мастерству',
    upright: 'Восьмерка показывает развитие, повторение навыка и ускорение событий.',
    reversed: 'Перевернутая Восьмерка говорит о сопротивлении переменам, усталости или остановке движения.',
    uprightKeywords: ['движение', 'мастерство', 'прогресс'],
    reversedKeywords: ['застой', 'сопротивление', 'утомление'],
  },
  {
    number: 9,
    label: 'Девятка',
    slug: 'devyatka',
    english: 'Nine',
    summary: 'приближение к итогу, личный результат и зрелость опыта',
    upright: 'Девятка показывает достижение, которое уже почти оформилось и требует осознания цены пути.',
    reversed: 'Перевернутая Девятка говорит о разочаровании, перегорании или страхе перед финальным шагом.',
    uprightKeywords: ['достижение', 'упорство', 'результат'],
    reversedKeywords: ['разочарование', 'перегорание', 'страх'],
  },
  {
    number: 10,
    label: 'Десятка',
    slug: 'desyatka',
    english: 'Ten',
    summary: 'завершение цикла и переход накопленного опыта в итог',
    upright: 'Десятка доводит тему масти до предела и показывает результат всего цикла.',
    reversed: 'Перевернутая Десятка говорит о перегрузке, нежеланном финале или затянутом завершении.',
    uprightKeywords: ['завершение', 'итог', 'цикл'],
    reversedKeywords: ['перегрузка', 'крах', 'усталость'],
  },
  {
    number: 11,
    label: 'Паж',
    slug: 'pazh',
    english: 'Page',
    summary: 'ученичество, послание и первый личный контакт с мастью',
    upright: 'Паж приносит любопытство, сообщение или начальную практику, где важно учиться.',
    reversed: 'Перевернутый Паж говорит о незрелости, откладывании или рассеянном интересе.',
    uprightKeywords: ['ученик', 'послание', 'новизна'],
    reversedKeywords: ['незрелость', 'промедление', 'рассеянность'],
  },
  {
    number: 12,
    label: 'Рыцарь',
    slug: 'rytsar',
    english: 'Knight',
    summary: 'движение, миссия и активное выражение масти',
    upright: 'Рыцарь показывает направленное действие и готовность защищать выбранный импульс.',
    reversed: 'Перевернутый Рыцарь говорит о безрассудстве, задержке или импульсивности без цели.',
    uprightKeywords: ['действие', 'движение', 'миссия'],
    reversedKeywords: ['безрассудство', 'задержка', 'импульсивность'],
  },
  {
    number: 13,
    label: 'Королева',
    slug: 'koroleva',
    english: 'Queen',
    summary: 'зрелая внутренняя мудрость и принимающая сила масти',
    upright: 'Королева показывает способность владеть энергией масти изнутри, мягко и устойчиво.',
    reversed: 'Перевернутая Королева говорит о холодности, подавленности или искаженном проявлении заботы.',
    uprightKeywords: ['зрелость', 'мудрость', 'забота'],
    reversedKeywords: ['холодность', 'мания', 'подавленность'],
  },
  {
    number: 14,
    label: 'Король',
    slug: 'korol',
    english: 'King',
    summary: 'мастерство, внешний авторитет и зрелое управление мастью',
    upright: 'Король показывает владение темой масти на уровне решения, ответственности и результата.',
    reversed: 'Перевернутый Король говорит о жесткости, злоупотреблении властью или упрямом контроле.',
    uprightKeywords: ['мастерство', 'лидерство', 'контроль'],
    reversedKeywords: ['жесткость', 'тирания', 'упрямство'],
  },
]

function unique(values) {
  return [...new Set(values)]
}

function buildMinorCards() {
  return suits.flatMap((suit) =>
    ranks.map((rank, index) => ({
      id: suit.firstId + index,
      number: rank.number,
      slug: `${rank.slug}-${suit.slugPart}`,
      name: `${rank.label} ${suit.genitive}`,
      nameEn: `${rank.english} of ${suit.english}`,
      aliases: [],
      summary: `${rank.summary} через ${suit.field}`,
      upright: `${rank.upright} В масти ${suit.genitive} это связано с полем: ${suit.field}.`,
      reversed: `${rank.reversed} Стихия масти - ${suit.element}, поэтому тень карты часто проявляется через перекос этой энергии.`,
      advice: suit.advice,
      uprightKeywords: unique([...rank.uprightKeywords, ...suit.themes]),
      reversedKeywords: rank.reversedKeywords,
      suitLabel: suit.label,
      suitSlug: suit.folder,
      element: suit.element,
      imagePath: `/cards/${suit.folder}/${rank.number.toString().padStart(2, '0')}.jpg`,
    })),
  )
}

function withDefaults(card) {
  const isMajor = card.id <= 22
  return {
    ...card,
    group: isMajor ? 'Старшие Арканы' : 'Младшие Арканы',
    suitLabel: card.suitLabel ?? 'Старшие Арканы',
    suitSlug: card.suitSlug ?? 'major',
    element: card.element ?? 'архетип',
    imagePath: card.imagePath ?? `/cards/major/${card.number.toString().padStart(2, '0')}.jpg`,
  }
}

function addRelated(cards) {
  const byNumberAndSuit = new Map(cards.map((card) => [`${card.suitSlug}:${card.number}`, card]))
  const byRank = new Map()
  for (const card of cards) {
    if (card.suitSlug === 'major') continue
    const list = byRank.get(card.number) ?? []
    list.push(card)
    byRank.set(card.number, list)
  }

  return cards.map((card, index) => {
    if (card.suitSlug === 'major') {
      return {
        ...card,
        relatedSlugs: [cards[index - 1]?.slug, cards[index + 1]?.slug].filter(Boolean),
      }
    }

    const sameSuit = [
      byNumberAndSuit.get(`${card.suitSlug}:${card.number - 1}`)?.slug,
      byNumberAndSuit.get(`${card.suitSlug}:${card.number + 1}`)?.slug,
    ]
    const sameRank = (byRank.get(card.number) ?? [])
      .filter((related) => related.slug !== card.slug)
      .slice(0, 2)
      .map((related) => related.slug)
    return {
      ...card,
      relatedSlugs: unique([...sameSuit, ...sameRank].filter(Boolean)),
    }
  })
}

export const TAROT_SEO_CARDS = addRelated([...majorCards.map(withDefaults), ...buildMinorCards().map(withDefaults)])

export const TAROT_SEO_SPREADS = [
  {
    slug: 'karta-dnya',
    label: 'Карта дня',
    cardCount: 1,
    summary: 'короткий ежедневный расклад для фокуса, предупреждения или внутренней настройки',
    description:
      'Карта дня отвечает на вопрос, с какой энергией стоит войти в ближайшие часы. Это не жесткий прогноз, а символический фокус: что заметить, где не спешить и какой внутренний ресурс взять с собой.',
    bestFor: ['ежедневная практика', 'быстрая самопроверка', 'вопрос на ближайшие часы'],
    positions: ['Главная энергия дня'],
  },
  {
    slug: 'tri-karty',
    label: 'Три карты',
    cardCount: 3,
    summary: 'классический мини-расклад для динамики ситуации: прошлое, настоящее и вероятный вектор',
    description:
      'Расклад из трех карт помогает увидеть ситуацию как движение. Первая карта показывает источник или прошлый фактор, вторая - текущую точку, третья - вероятное развитие, если сохранить текущую линию поведения.',
    bestFor: ['отношения', 'работа', 'выбор между вариантами', 'разбор причины и следствия'],
    positions: ['Прошлое', 'Настоящее', 'Будущее'],
  },
  {
    slug: 'keltskiy-krest',
    label: 'Кельтский крест',
    cardCount: 10,
    summary: 'глубокий расклад для сложных вопросов, внутренних влияний и внешнего контекста',
    description:
      'Кельтский крест раскрывает не только событие, но и его скрытую механику: вызов, основание, недавнее прошлое, ближайшее будущее, позицию человека, окружение, надежды, страхи и итоговый вектор.',
    bestFor: ['жизненные развилки', 'сложные отношения', 'долгие проекты', 'вопросы с большим контекстом'],
    positions: ['Ситуация', 'Вызов', 'Основание', 'Недавнее прошлое', 'Сознательная цель', 'Ближайшее будущее', 'Позиция человека', 'Окружение', 'Надежды и страхи', 'Итог'],
  },
]

export const TAROT_SEO_DECKS = [
  {
    slug: 'rider-waite-smith',
    label: 'Rider-Waite-Smith',
    period: '1909',
    summary: 'классическая школа с ясными сюжетами и универсальным языком современного Таро',
    description:
      'Rider-Waite-Smith стала основной визуальной грамматикой для большинства современных колод. Ее сила - в сюжетных Младших Арканах, понятных символах и балансе бытового, психологического и эзотерического уровня.',
    bestFor: ['первые расклады', 'отношения', 'работа', 'универсальные вопросы'],
  },
  {
    slug: 'thoth',
    label: 'Thoth',
    period: '1938-1943',
    summary: 'оккультная колода Кроули и Харрис с каббалой, астрологией и алхимическими соответствиями',
    description:
      'Thoth обращается к сложной символической системе, где цвет, число, буква, планета и знак складываются в плотный эзотерический язык. Она подходит для глубинных вопросов и тонкой психологической работы.',
    bestFor: ['духовные вопросы', 'психологический анализ', 'символические исследования'],
  },
  {
    slug: 'marseille',
    label: 'Marseille',
    period: 'XVII-XVIII век',
    summary: 'историческая европейская школа с лаконичными Младшими Арканами и числовой символикой',
    description:
      'Марсельская традиция не навязывает сюжет в Младших Арканах. Она требует читать число, масть, композицию и контекст вопроса, поэтому часто воспринимается строже и суше, чем Waite-Smith.',
    bestFor: ['нумерология', 'историческая практика', 'строгие расклады'],
  },
  {
    slug: 'visconti-sforza',
    label: 'Visconti-Sforza',
    period: 'XV век',
    summary: 'ренессансная придворная эстетика и одна из самых ранних известных традиций Таро',
    description:
      'Visconti-Sforza несет атмосферу ренессансной миниатюры, золота и придворного символизма. В ней меньше современной психологии и больше архетипической, исторической и родовой образности.',
    bestFor: ['исторические вопросы', 'родовые темы', 'медитативная практика'],
  },
  {
    slug: 'modern-witch',
    label: 'Modern Witch',
    period: '2019',
    summary: 'современное переосмысление Waite-Smith с городской, инклюзивной и живой визуальностью',
    description:
      'Modern Witch сохраняет символический каркас Waite-Smith, но переносит его в современный культурный контекст. Благодаря этому карты легче считываются в вопросах идентичности, творчества и городской повседневности.',
    bestFor: ['современные отношения', 'самоидентичность', 'творчество', 'повседневные решения'],
  },
]

export const FAQ_ITEMS = [
  {
    question: 'Как правильно сформулировать вопрос для расклада Таро?',
    answer:
      'Лучше задавать открытый вопрос о ситуации и своем участии в ней: не "он вернется?", а "что мне важно понять об этой связи?". Так расклад показывает не только событие, но и пространство выбора.',
  },
  {
    question: 'Что означает перевернутая карта?',
    answer:
      'Перевернутая карта не всегда означает плохой итог. Чаще она показывает внутренний блок, задержку, чрезмерность качества или скрытую сторону прямого значения.',
  },
  {
    question: 'Чем отличается карта дня от расклада на три карты?',
    answer:
      'Карта дня дает один фокус на ближайшее время. Три карты показывают динамику: откуда пришла ситуация, где она сейчас и куда может развиваться.',
  },
  {
    question: 'Можно ли спрашивать Таро про отношения?',
    answer:
      'Да, но полезнее спрашивать не о контроле другого человека, а о структуре связи, своих чувствах, границах и возможном следующем шаге.',
  },
  {
    question: 'Можно ли использовать Таро для вопросов о работе и деньгах?',
    answer:
      'Да. В таких вопросах карты помогают увидеть мотивацию, риски, ресурсы, коммуникацию и стратегию, но не заменяют финансовый или юридический совет.',
  },
  {
    question: 'AI-интерпретация заменяет таролога?',
    answer:
      'Нет. AI помогает быстро собрать символы в связный текст и подсветить возможные смыслы. Ответственность за решение остается за человеком.',
  },
]

function cardRoute(card) {
  return {
    name: `tarot-card-${card.slug}`,
    path: `/tarot/cards/${card.slug}`,
    title: `${card.name}: значение карты Таро | Вуаль Грядущего`,
    description: `Значение карты ${card.name}: ${card.summary}. Прямое и перевернутое положение, ключевые темы и связи в раскладах.`,
    priority: card.suitSlug === 'major' ? 0.74 : 0.68,
    changefreq: 'monthly',
    type: 'article',
    contentKind: 'card',
    slug: card.slug,
  }
}

function spreadRoute(spread) {
  return {
    name: `tarot-spread-${spread.slug}`,
    path: `/tarot/spreads/${spread.slug}`,
    title: `${spread.label}: расклад Таро | Вуаль Грядущего`,
    description: `${spread.label} - ${spread.summary}. Позиции карт, когда использовать расклад и как читать результат.`,
    priority: 0.78,
    changefreq: 'monthly',
    type: 'article',
    contentKind: 'spread',
    slug: spread.slug,
  }
}

function deckRoute(deck) {
  return {
    name: `tarot-deck-${deck.slug}`,
    path: `/tarot/decks/${deck.slug}`,
    title: `${deck.label}: колода Таро | Вуаль Грядущего`,
    description: `${deck.label} - ${deck.summary}. История, символика и подходящие вопросы для раскладов.`,
    priority: 0.7,
    changefreq: 'monthly',
    type: 'article',
    contentKind: 'deck',
    slug: deck.slug,
  }
}

export const SEO_CONTENT_ROUTES = [
  {
    name: 'faq',
    path: '/faq',
    title: 'FAQ по Таро и онлайн-раскладам | Вуаль Грядущего',
    description: 'Ответы на частые вопросы о раскладах Таро: как формулировать вопрос, что значат перевернутые карты и как читать результат.',
    priority: 0.76,
    changefreq: 'monthly',
    type: 'article',
    contentKind: 'faq',
    slug: 'faq',
  },
  ...TAROT_SEO_SPREADS.map(spreadRoute),
  ...TAROT_SEO_DECKS.map(deckRoute),
  ...TAROT_SEO_CARDS.map(cardRoute),
]

export function findTarotSeoCardBySlug(slug) {
  return TAROT_SEO_CARDS.find((card) => card.slug === slug)
}

export function findTarotSeoCardById(id) {
  return TAROT_SEO_CARDS.find((card) => card.id === id)
}

export function findTarotSeoSpreadBySlug(slug) {
  return TAROT_SEO_SPREADS.find((spread) => spread.slug === slug)
}

export function findTarotSeoDeckBySlug(slug) {
  return TAROT_SEO_DECKS.find((deck) => deck.slug === slug)
}

export function findSeoContentRouteByPath(path) {
  const normalized = normalizePath(path)
  return SEO_CONTENT_ROUTES.find((route) => normalizePath(route.path) === normalized)
}

function normalizePath(path) {
  if (!path || path === '/') return '/'
  return path.replace(/\/+$/, '')
}

function absoluteUrl(siteUrl, path) {
  return `${siteUrl.replace(/\/+$/, '')}${path.startsWith('/') ? path : `/${path}`}`
}

function breadcrumbList(items) {
  return {
    '@context': 'https://schema.org',
    '@type': 'BreadcrumbList',
    itemListElement: items.map((item, index) => ({
      '@type': 'ListItem',
      position: index + 1,
      name: item.name,
      item: item.url,
    })),
  }
}

function article(route, context, extra = {}) {
  const url = absoluteUrl(context.siteUrl, route.path)
  return {
    '@context': 'https://schema.org',
    '@type': 'Article',
    headline: route.title,
    description: route.description,
    inLanguage: 'ru-RU',
    mainEntityOfPage: url,
    image: absoluteUrl(context.siteUrl, context.defaultImage),
    author: {
      '@type': 'Organization',
      name: context.siteName,
    },
    publisher: {
      '@type': 'Organization',
      name: context.siteName,
      logo: {
        '@type': 'ImageObject',
        url: absoluteUrl(context.siteUrl, '/icons/icon-512.png'),
      },
    },
    ...extra,
  }
}

export function buildStructuredDataForRoute(route, context) {
  if (!route) return []
  const url = absoluteUrl(context.siteUrl, route.path)
  const baseWebPage = {
    '@context': 'https://schema.org',
    '@type': 'WebPage',
    name: route.title,
    description: route.description,
    url,
    inLanguage: 'ru-RU',
    isPartOf: {
      '@type': 'WebSite',
      name: context.siteName,
      url: context.siteUrl,
    },
  }

  if (route.contentKind === 'faq') {
    return [
      baseWebPage,
      breadcrumbList([
        { name: context.siteName, url: absoluteUrl(context.siteUrl, '/') },
        { name: 'FAQ', url },
      ]),
      {
        '@context': 'https://schema.org',
        '@type': 'FAQPage',
        mainEntity: FAQ_ITEMS.map((item) => ({
          '@type': 'Question',
          name: item.question,
          acceptedAnswer: {
            '@type': 'Answer',
            text: item.answer,
          },
        })),
      },
    ]
  }

  const source =
    route.contentKind === 'card'
      ? findTarotSeoCardBySlug(route.slug)
      : route.contentKind === 'spread'
        ? findTarotSeoSpreadBySlug(route.slug)
        : route.contentKind === 'deck'
          ? findTarotSeoDeckBySlug(route.slug)
          : null

  const crumbName =
    route.contentKind === 'card'
      ? source?.name
      : route.contentKind === 'spread' || route.contentKind === 'deck'
        ? source?.label
        : route.title

  const sectionName =
    route.contentKind === 'card'
      ? 'Карты Таро'
      : route.contentKind === 'spread'
        ? 'Расклады Таро'
        : route.contentKind === 'deck'
          ? 'Колоды Таро'
          : 'Глоссарий'

  return [
    baseWebPage,
    breadcrumbList([
      { name: context.siteName, url: absoluteUrl(context.siteUrl, '/') },
      { name: sectionName, url: absoluteUrl(context.siteUrl, '/glossary') },
      { name: crumbName ?? route.title, url },
    ]),
    article(route, context, {
      about: source
        ? {
            '@type': 'Thing',
            name: source.name ?? source.label,
            description: source.summary,
          }
        : undefined,
    }),
  ]
}
