namespace Jnx.Modules

open Nancy

module Utils =
    let (?<-) (viewBag:obj) (name:string) (value:'T) =
        (viewBag :?> DynamicDictionary).Add(name, box value)

    let (?) (args:obj) (name:string) =
        ((args :?> DynamicDictionary).[name] :?> DynamicDictionaryValue).Value
