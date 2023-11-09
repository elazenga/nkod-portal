import { useState } from "react";
import Button from "./Button";
import BaseInput from "./BaseInput";
import GridRow from "./GridRow";
import GridColumn from "./GridColumn";

type Props = 
{
    values: string[];
    onChange: (items: string[]) => void;
    disabled?: boolean;
    id: string;
}

export default function MultiTextBox(props: Props)
{
    const [newValue, newValueChanged] = useState<string>('');
    const { values, onChange, disabled, ...rest } = props;

    return <><GridRow>
        <GridColumn widthUnits={3} totalUnits={4}>
            <BaseInput value={newValue} onChange={e => newValueChanged(e.target.value)} disabled={disabled} {...rest} />
        </GridColumn>
        <GridColumn widthUnits={1} totalUnits={4}>
            <Button buttonType="secondary"  onClick={e => {
                e.preventDefault();
                if (newValue !== '') {
                    if (!values.includes(newValue)) {
                        onChange([...values, newValue]);
                    }
                }}} disabled={newValue === '' || disabled}>
                Pridať
            </Button>
        </GridColumn>
    </GridRow>
    <div>
        {values.length > 0 ? <div className="nkod-entity-detail"><div className="nkod-entity-detail-tags govuk-clearfix">
                {values.map(o => <div key={o} data-value={o} className="govuk-body nkod-entity-detail-tag" style={{cursor: 'pointer'}} onClick={() => {
                    if (!disabled) {
                        onChange(values.filter(x => x !== o));
                    }
                }}>
                    <span>
                    {o} <span style={{marginLeft: '10px'}}>x</span>
                    </span>
                </div>)}
            </div></div> : null}
    </div></>
}